using Microsoft.EntityFrameworkCore.Migrations;

namespace OneLoaf.Infrastructure.Persistence.Migrations;

/// <summary>
/// Runs SQL scripts that are kept as .sql files next to the migrations
/// (Persistence/Migrations/Sql/&lt;MigrationName&gt;/*.sql) and embedded into the assembly.
/// A script belongs to exactly one migration and is never edited after release:
/// a later change to a function or view is a new script in a new migration (CREATE OR REPLACE).
/// </summary>
internal static class MigrationSql
{
    public static void SqlScripts(this MigrationBuilder migrationBuilder, string migrationFolder, params string[] fileNames)
    {
        foreach (var file in fileNames)
            migrationBuilder.Sql(Read($"{migrationFolder}/{file}"));
    }

    private static string Read(string relativePath)
    {
        var assembly = typeof(MigrationSql).Assembly;
        var wanted = "Sql/" + relativePath;
        var name = assembly.GetManifestResourceNames()
                       .SingleOrDefault(n => string.Equals(n.Replace('\\', '/'), wanted, StringComparison.OrdinalIgnoreCase))
                   ?? throw new InvalidOperationException($"Embedded SQL script '{wanted}' not found.");

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
