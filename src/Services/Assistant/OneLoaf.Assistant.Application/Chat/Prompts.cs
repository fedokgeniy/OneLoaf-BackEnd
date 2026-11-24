using System.Globalization;

namespace OneLoaf.Assistant.Application.Chat;

/// <summary>Embedded prompt texts.</summary>
public static class Prompts
{
    /// <summary>System prompt of the chat.</summary>
    public static string System { get; } = Read("Prompts.system.md");

    /// <summary>System prompt of the bake analysis.</summary>
    public static string Analysis { get; } = Read("Prompts.analysis.md");

    /// <summary>Disclaimer for general advice (no tool was used).</summary>
    public const string AdviceDisclaimer =
        "General information, not based on your data. No guarantee of food safety or allergen content.";

    /// <summary>Correction sent once when the answer contains numbers without a source.</summary>
    public const string NumberCorrection =
        "Your answer contains numbers that no tool returned: {0}. Rewrite the answer using only numbers from the tool results; call a tool if a number is missing.";

    /// <summary>Answer when the tool round limit is reached.</summary>
    public const string RoundLimitAnswer =
        "I could not finish this request in a reasonable number of steps. Please rephrase it or use the planning form.";

    /// <summary>Answer when the safety system declined the request.</summary>
    public const string RefusalAnswer = "I can't help with that request.";

    /// <summary>Title of the confirmation message.</summary>
    public const string BatchSavedMessage = "The batch is saved. You can follow it in the production calendar.";

    /// <summary>Confirmation message of a saved recipe.</summary>
    public const string RecipeSavedMessage =
        "The recipe is saved. New ingredients have no price yet: add prices to see what a loaf costs.";

    private static string Read(string name)
    {
        using var stream = typeof(Prompts).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded prompt '{name}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Per-request context: the baker's local date and time zone.</summary>
    public static string Context(DateTimeOffset now, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);
        var local = TimeZoneInfo.ConvertTime(now, zone);
        return string.Create(CultureInfo.InvariantCulture,
            $"Today is {local:yyyy-MM-dd dddd}, the time is {local:HH:mm}, the baker's time zone is {zone.Id} (UTC offset {local:zzz}).");
    }
}
