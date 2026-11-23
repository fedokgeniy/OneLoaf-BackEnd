using System.Globalization;
using System.Text;
using OneLoaf.SharedKernel.Security;

namespace OneLoaf.CoreApi.Application.Export;

/// <summary>A recipe row of the CSV export.</summary>
/// <param name="Title">Title.</param>
/// <param name="IsVariant">Variant of another recipe.</param>
/// <param name="YieldLoaves">Loaves of the yield.</param>
/// <param name="LoafWeightG">Loaf weight.</param>
/// <param name="IngredientLines">Own ingredient lines.</param>
/// <param name="StageLines">Own stage lines.</param>
/// <param name="IsArchived">Archived.</param>
/// <param name="CreatedAt">Creation time.</param>
/// <param name="UpdatedAt">Last change.</param>
public sealed record RecipeExportRow(
    string Title, bool IsVariant, int YieldLoaves, decimal? LoafWeightG, int IngredientLines, int StageLines, bool IsArchived,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

/// <summary>A bake record row of the CSV export.</summary>
/// <param name="BakedOn">Baking date.</param>
/// <param name="Recipes">Recipe titles with loaves.</param>
/// <param name="TotalLoaves">Loaves.</param>
/// <param name="TotalCost">Cost.</param>
/// <param name="Currency">Currency.</param>
/// <param name="Rating">Rating 1-5.</param>
/// <param name="Comment">Baker's comment.</param>
public sealed record BakeRecordExportRow(
    DateOnly BakedOn, string Recipes, int TotalLoaves, decimal? TotalCost, string Currency, short? Rating, string? Comment);

/// <summary>Reads the user's data for the export.</summary>
public interface IExportReader
{
    /// <summary>All own recipes by title.</summary>
    Task<IReadOnlyList<RecipeExportRow>> GetRecipesAsync(Guid ownerId, CancellationToken cancellationToken);

    /// <summary>All own bake records, newest first.</summary>
    Task<IReadOnlyList<BakeRecordExportRow>> GetBakeRecordsAsync(Guid ownerId, CancellationToken cancellationToken);
}

/// <summary>CSV export of the own recipes and bake records (US-50); RFC 4180, UTF-8 with BOM for spreadsheet apps.</summary>
public sealed class ExportService(IExportReader reader, ICurrentUser currentUser)
{
    /// <summary>Media type of the files.</summary>
    public const string CsvContentType = "text/csv; charset=utf-8";

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);

    /// <summary>Recipes as CSV.</summary>
    public async Task<byte[]> RecipesCsvAsync(CancellationToken cancellationToken)
    {
        var rows = await reader.GetRecipesAsync(currentUser.Id, cancellationToken);
        return Csv.Write(
            ["title", "is_variant", "yield_loaves", "loaf_weight_g", "ingredient_lines", "stage_lines", "is_archived", "created_at", "updated_at"],
            rows.Select(r => new object?[]
            {
                r.Title, r.IsVariant, r.YieldLoaves, r.LoafWeightG, r.IngredientLines, r.StageLines, r.IsArchived, r.CreatedAt, r.UpdatedAt,
            }),
            Utf8WithBom);
    }

    /// <summary>Bake records as CSV.</summary>
    public async Task<byte[]> BakeRecordsCsvAsync(CancellationToken cancellationToken)
    {
        var rows = await reader.GetBakeRecordsAsync(currentUser.Id, cancellationToken);
        return Csv.Write(
            ["baked_on", "recipes", "total_loaves", "total_cost", "currency", "rating", "comment"],
            rows.Select(r => new object?[] { r.BakedOn, r.Recipes, r.TotalLoaves, r.TotalCost, r.Currency, r.Rating, r.Comment }),
            Utf8WithBom);
    }
}

/// <summary>Minimal RFC 4180 writer with invariant formatting.</summary>
public static class Csv
{
    private static readonly char[] QuotedCharacters = [',', '"', '\r', '\n'];

    // Cells starting with these are evaluated as formulas by spreadsheet apps.
    private static readonly char[] FormulaPrefixes = ['=', '+', '-', '@'];

    /// <summary>Writes the header and the rows.</summary>
    public static byte[] Write(IReadOnlyList<string> header, IEnumerable<object?[]> rows, Encoding encoding)
    {
        ArgumentNullException.ThrowIfNull(encoding);
        var text = new StringBuilder();
        text.AppendJoin(',', header).Append("\r\n");
        foreach (var row in rows)
        {
            text.AppendJoin(',', row.Select(Format)).Append("\r\n");
        }

        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text.ToString())];
    }

    /// <summary>One cell: invariant culture, ISO 8601 dates, quoted when needed, formula injection neutralised.</summary>
    public static string Format(object? value)
    {
        var text = value switch
        {
            null => string.Empty,
            bool b => b ? "true" : "false",
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
        if (value is string && text.Length > 0 && Array.IndexOf(FormulaPrefixes, text[0]) >= 0)
        {
            text = "'" + text;
        }

        return text.IndexOfAny(QuotedCharacters) >= 0 ? "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : text;
    }
}
