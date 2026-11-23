namespace OneLoaf.ServiceDefaults.Constants;

/// <summary>Names of the meters exported on <c>/metrics</c>.</summary>
public static class Meters
{
    /// <summary>Custom business meter of OneLoaf (<c>oneloaf.*</c> instruments).</summary>
    public const string OneLoaf = "OneLoaf";

    /// <summary>Connection pool and command metrics published by Npgsql.</summary>
    public const string Npgsql = "Npgsql";
}
