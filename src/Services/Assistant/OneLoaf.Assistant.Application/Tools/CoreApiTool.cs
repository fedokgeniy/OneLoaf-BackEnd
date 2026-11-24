using System.Text.Json;
using OneLoaf.Assistant.Application.Claude;

namespace OneLoaf.Assistant.Application.Tools;

/// <summary>A tool that is one read call to the Core API. The Core API checks ownership and does every calculation.</summary>
public abstract class CoreApiTool(ICoreApiClient coreApi) : IAssistantTool
{
    /// <summary>Upper bound of loaves accepted from the model.</summary>
    protected const int MaxLoaves = 1000;

    /// <inheritdoc />
    public abstract ClaudeToolDefinition Definition { get; }

    /// <inheritdoc />
    public async Task<ToolResult> ExecuteAsync(JsonElement input, ToolContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        var path = BuildPath(input, out var problem);
        if (path is null)
        {
            return ToolResult.Error(problem ?? "Invalid input.");
        }

        return ToResult(await coreApi.SendAsync(HttpMethod.Get, path, null, cancellationToken));
    }

    /// <summary>The path and query for the input, or null with an problem for invalid input.</summary>
    protected abstract string? BuildPath(JsonElement input, out string? problem);

    /// <summary>Turns a Core API response into a tool result (problem details become an problem the model can explain).</summary>
    internal static ToolResult ToResult(CoreApiResponse response)
    {
        if (response.IsSuccess)
        {
            return new ToolResult(response.Body, false);
        }

        var title = "The request failed.";
        try
        {
            using var problem = JsonDocument.Parse(response.Body);
            if (problem.RootElement.TryGetProperty("title", out var t) && t.GetString() is { } text)
            {
                title = problem.RootElement.TryGetProperty("detail", out var d) && d.GetString() is { } detail ? $"{text} {detail}" : text;
            }
        }
        catch (JsonException)
        {
            // Not a problem document; keep the generic message.
        }

        return ToolResult.Error($"{(int)response.Status}: {title}");
    }
}
