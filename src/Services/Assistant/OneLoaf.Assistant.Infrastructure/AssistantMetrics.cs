using System.Diagnostics.Metrics;
using OneLoaf.ServiceDefaults.Constants;

namespace OneLoaf.Assistant.Infrastructure;

/// <summary>Counters of the assistant (<c>oneloaf.ai.requests</c>, <c>oneloaf.ai.tokens</c>, <c>oneloaf.ai.cost.usd</c>).</summary>
public sealed class AssistantMetrics
{
    private readonly Counter<long> _requests;
    private readonly Counter<long> _tokens;
    private readonly Counter<double> _cost;

    /// <summary>Creates the counters on the OneLoaf meter.</summary>
    public AssistantMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(Meters.OneLoaf);
        _requests = meter.CreateCounter<long>("oneloaf.ai.requests", description: "Assistant turns sent to Claude.");
        _tokens = meter.CreateCounter<long>("oneloaf.ai.tokens", description: "Tokens billed by Claude.");
        _cost = meter.CreateCounter<double>("oneloaf.ai.cost.usd", description: "Cost of the assistant by the configured prices.");
    }

    /// <summary>A finished turn.</summary>
    public void Turn(long tokens, decimal costUsd)
    {
        _requests.Add(1);
        _tokens.Add(tokens);
        _cost.Add((double)costUsd);
    }
}
