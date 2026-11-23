using System.Text.Json;
using Microsoft.Extensions.Logging;
using OneLoaf.Assistant.Application.Budget;
using OneLoaf.Assistant.Application.Chat;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Assistant.Application.Verification;
using OneLoaf.SharedKernel.Exceptions;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.Assistant.Application.Analysis;

/// <summary>Result of a bake analysis (US-36).</summary>
/// <param name="BakeRecordId">Record.</param>
/// <param name="Analysis">Text stored in the record.</param>
/// <param name="UnverifiedNumbers">Numbers removed because no tool produced them.</param>
public sealed record AnalysisDto(Guid BakeRecordId, string Analysis, IReadOnlyList<string> UnverifiedNumbers);

/// <summary>
/// UC-02: analyses a finished bake from its stage log and feedback and stores the text in the record. Requires
/// feedback (422 otherwise); when Claude is unavailable the record is not changed (503).
/// </summary>
public sealed partial class AnalysisService(
    IClaudeClient claude,
    ToolRegistry tools,
    ICoreApiClient coreApi,
    UsageGuard usageGuard,
    TimeProvider timeProvider,
    ICurrentUser currentUser,
    ILogger<AnalysisService> logger)
{
    /// <summary>Message when the record has no feedback.</summary>
    public const string NoFeedbackMessage = "Add a rating comment to the bake record before asking for an analysis.";

    /// <summary>Message for a missing record.</summary>
    public const string NotFoundMessage = "The bake record was not found.";

    private const int MaxRounds = 3;

    /// <summary>Runs the analysis and stores it.</summary>
    public async Task<AnalysisDto> AnalyseAsync(Guid bakeRecordId, CancellationToken cancellationToken)
    {
        var record = await coreApi.SendAsync(HttpMethod.Get, $"{CoreApiRoutes.BakeRecords}/{bakeRecordId}", null, cancellationToken);
        if (!record.IsSuccess)
        {
            throw new NotFoundException(NotFoundMessage);
        }

        using (var json = JsonDocument.Parse(record.Body))
        {
            if (!json.RootElement.TryGetProperty("comment", out var comment) || string.IsNullOrWhiteSpace(comment.GetString()))
            {
                throw new BusinessRuleException(NoFeedbackMessage);
            }
        }

        await usageGuard.EnsureAllowedAsync(cancellationToken);
        var context = new ToolContext(currentUser.Id, Guid.Empty);
        var outputs = new List<string>();
        int input = 0, output = 0, cacheRead = 0;
        var messages = new List<ClaudeMessage>
        {
            ClaudeMessage.Text(ClaudeRole.User, $"Analyse the bake record <user_data>{bakeRecordId}</user_data>."),
        };
        var request = new ClaudeRequest(Prompts.Analysis, Prompts.Context(timeProvider.GetUtcNow(), TimeZoneInfo.Utc),
            tools.DefinitionsFor(ToolNames.GetBakeAnalysisData), messages);

        var text = string.Empty;
        for (var round = 0; round < MaxRounds; round++)
        {
            var response = await claude.SendAsync(request with { Messages = messages }, cancellationToken);
            input += response.InputTokens;
            output += response.OutputTokens;
            cacheRead += response.CacheReadTokens;
            if (response.StopReason != StopReasons.ToolUse)
            {
                text = response.StopReason == StopReasons.Refusal ? Prompts.RefusalAnswer : response.Text;
                break;
            }

            messages.Add(new ClaudeMessage(ClaudeRole.Assistant, response.Content));
            var results = new List<ClaudeBlock>();
            foreach (var use in response.ToolUses)
            {
                var result = await tools.ExecuteAsync(use.Name, use.Input, context, cancellationToken);
                if (!result.IsError)
                {
                    outputs.Add(result.Content);
                }

                results.Add(new ClaudeToolResultBlock(use.Id, result.Content, result.IsError));
            }

            messages.Add(new ClaudeMessage(ClaudeRole.User, results));
        }

        var check = NumberProvenanceVerifier.Verify(text, outputs, [bakeRecordId.ToString()], TimeZoneInfo.Utc);
        var analysis = NumberProvenanceVerifier.Redact(text, check.Unverified);
        var saved = await coreApi.SendAsync(HttpMethod.Put, $"{CoreApiRoutes.BakeRecords}/{bakeRecordId}/analysis",
            new { analysis }, cancellationToken);
        if (!saved.IsSuccess)
        {
            throw new ConflictException(CoreApiTool.ToResult(saved).Content);
        }

        var cost = usageGuard.Cost(input, output, cacheRead);
        await usageGuard.RecordAsync(input, output, cost, cancellationToken);
        LogAnalysed(logger, bakeRecordId, input, output);
        return new AnalysisDto(bakeRecordId, analysis, check.Unverified);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Bake record {BakeRecordId} analysed: {InputTokens} in / {OutputTokens} out")]
    private static partial void LogAnalysed(ILogger logger, Guid bakeRecordId, int inputTokens, int outputTokens);
}
