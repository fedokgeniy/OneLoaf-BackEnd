using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.Assistant.Infrastructure.Claude;

/// <summary>Claude settings (<c>Anthropic__ApiKey</c>, <c>Anthropic__Model</c>, ...).</summary>
public sealed class AnthropicOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Anthropic";

    /// <summary>API key; kept only in the environment.</summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Model id; switching models needs no code change.</summary>
    [Required]
    public string Model { get; set; } = "claude-haiku-5-5";

    /// <summary>Output token limit per request.</summary>
    [Range(256, 64_000)]
    public int MaxTokens { get; set; } = 1024;

    /// <summary>HTTP timeout per request.</summary>
    [Range(1, 600)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>Retries of 429/5xx/network errors by the SDK.</summary>
    [Range(0, 5)]
    public int MaxRetries { get; set; } = 1;
}

/// <summary>
/// <see cref="IClaudeClient"/> over the official Anthropic SDK. The system prompt and the tool definitions are marked
/// for prompt caching (they are identical across requests); the per-request context follows the cached prefix.
/// Transport failures become 503 for the client (UC-01 alternative flow A3).
/// </summary>
public sealed class AnthropicClaudeClient : IClaudeClient, IDisposable
{
    private const string UnavailableMessage = "The assistant is temporarily unavailable. Use the planning form or try again later.";
    private readonly AnthropicClient _client;
    private readonly AnthropicOptions _options;

    /// <summary>Creates the SDK client from the options.</summary>
    public AnthropicClaudeClient(IOptions<AnthropicOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
        _client = new AnthropicClient
        {
            ApiKey = _options.ApiKey,
            Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds),
            MaxRetries = _options.MaxRetries,
        };
    }

    /// <inheritdoc />
    public async Task<ClaudeResponse> SendAsync(ClaudeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var parameters = new MessageCreateParams
        {
            Model = _options.Model,
            MaxTokens = _options.MaxTokens,

            // Claude Haiku 5.5 thinks by default. The assistant answers short questions with tool calls and replays only
            // text and tool blocks, so thinking stays off: nothing of the token limit goes to it and no thinking block
            // has to be passed back with the tool results.
            Thinking = new ThinkingConfigDisabled(),
            System = new List<TextBlockParam>
            {
                new() { Text = request.System, CacheControl = new CacheControlEphemeral() },
                new() { Text = request.Context },
            },
            Tools = request.Tools.Select((tool, index) => (ToolUnion)ToTool(tool, index == request.Tools.Count - 1)).ToList(),
            Messages = request.Messages.Select(ToParam).ToList(),
        };

        try
        {
            var message = await _client.Messages.Create(parameters, cancellationToken);
            return new ClaudeResponse(
                message.StopReason?.Raw() ?? StopReasons.EndTurn,
                message.Content.Select(FromBlock).OfType<ClaudeBlock>().ToList(),
                (int)message.Usage.InputTokens,
                (int)message.Usage.OutputTokens,
                (int)(message.Usage.CacheReadInputTokens ?? 0));
        }
        catch (Exception ex) when (ex is AnthropicRateLimitException or Anthropic5xxException or AnthropicIOException
                                       or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            throw new ExternalServiceUnavailableException(UnavailableMessage, ex);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();

    private static Tool ToTool(ClaudeToolDefinition definition, bool last)
    {
        var properties = definition.InputSchema.TryGetProperty("properties", out var p)
            ? p.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
            : [];
        var required = definition.InputSchema.TryGetProperty("required", out var r)
            ? r.EnumerateArray().Select(x => x.GetString()!).ToList()
            : [];
        return new Tool
        {
            Name = definition.Name,
            Description = definition.Description,
            InputSchema = new() { Properties = properties, Required = required },
            CacheControl = last ? new CacheControlEphemeral() : null,
        };
    }

    private static MessageParam ToParam(ClaudeMessage message)
    {
        var blocks = message.Blocks.Select(block => block switch
        {
            ClaudeTextBlock text => (ContentBlockParam)new TextBlockParam { Text = text.Text },
            ClaudeToolUseBlock use => new ToolUseBlockParam
            {
                ID = use.Id,
                Name = use.Name,
                Input = use.Input.ValueKind == JsonValueKind.Object
                    ? use.Input.EnumerateObject().ToDictionary(x => x.Name, x => x.Value.Clone())
                    : new Dictionary<string, JsonElement>(),
            },
            ClaudeToolResultBlock result => new ToolResultBlockParam
            {
                ToolUseID = result.ToolUseId,
                Content = result.Content,
                IsError = result.IsError,
            },
            _ => throw new NotSupportedException(block.GetType().Name),
        }).ToList();

        return message.Role == ClaudeRole.User
            ? new UserMessageParam { Content = blocks }
            : new AssistantMessageParam { Content = blocks };
    }

    private static ClaudeBlock? FromBlock(ContentBlock block)
    {
        if (block.TryPickText(out TextBlock? text))
        {
            return new ClaudeTextBlock(text.Text);
        }

        if (block.TryPickToolUse(out ToolUseBlock? use))
        {
            return new ClaudeToolUseBlock(use.ID, use.Name, JsonSerializer.SerializeToElement(use.Input));
        }

        return null;
    }
}
