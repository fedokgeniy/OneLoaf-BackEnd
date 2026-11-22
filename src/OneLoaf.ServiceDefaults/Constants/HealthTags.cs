namespace OneLoaf.ServiceDefaults.Constants;

/// <summary>Tags that assign health checks to the liveness or readiness endpoint.</summary>
public static class HealthTags
{
    /// <summary>Checks of the process itself (<c>/health/live</c>).</summary>
    public const string Live = "live";

    /// <summary>Checks of external dependencies (<c>/health/ready</c>).</summary>
    public const string Ready = "ready";
}
