using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace OneLoaf.Infrastructure.Persistence;

/// <summary>
/// Model-wide conventions so that the generated PostgreSQL schema is readable without quoting:
/// snake_case names for columns, keys, foreign keys and indexes; enums stored as snake_case text
/// with a CHECK constraint; uuid and timestamp defaults.
/// </summary>
public static class ModelConventions
{
    public static void ApplyOneLoafConventions(this ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            var table = entity.GetTableName()!;

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(ToSnakeCase(property.Name));

                if (property.Name is "CreatedAt" or "UpdatedAt" && property.ClrType == typeof(DateTime))
                    property.SetDefaultValueSql("now()");

                if (property.IsPrimaryKey() && property.Name == "Id" && property.ClrType == typeof(Guid))
                    property.SetDefaultValueSql("gen_random_uuid()");

                var enumType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (enumType.IsEnum)
                {
                    var converterType = typeof(SnakeCaseEnumConverter<>).MakeGenericType(enumType);
                    property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                    property.SetMaxLength(32);

                    var values = string.Join(", ", Enum.GetNames(enumType).Select(n => $"'{ToSnakeCase(n)}'"));
                    var column = property.GetColumnName();
                    entity.AddCheckConstraint($"ck_{table}_{column}", $"{column} IN ({values})");
                }
            }

            foreach (var key in entity.GetKeys())
                key.SetName(key.IsPrimaryKey()
                    ? $"pk_{table}"
                    : $"ak_{table}_{string.Join("_", key.Properties.Select(p => p.GetColumnName()))}");

            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(
                    $"fk_{table}_{fk.PrincipalEntityType.GetTableName()}_{string.Join("_", fk.Properties.Select(p => p.GetColumnName()))}");

            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(index.Name ??
                    $"{(index.IsUnique ? "ux" : "ix")}_{table}_{string.Join("_", index.Properties.Select(p => p.GetColumnName()))}");
        }
    }

    public static string ToSnakeCase(string name)
    {
        var sb = new StringBuilder(name.Length + 8);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                var prevIsLowerOrDigit = i > 0 && (char.IsLower(name[i - 1]) || char.IsDigit(name[i - 1]));
                var nextIsLower = i + 1 < name.Length && char.IsLower(name[i + 1]);
                if (i > 0 && (prevIsLowerOrDigit || (nextIsLower && char.IsUpper(name[i - 1]))))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }
}

/// <summary>Stores an enum as snake_case text: StageType.BulkFermentation &lt;-&gt; 'bulk_fermentation'.</summary>
public sealed class SnakeCaseEnumConverter<TEnum> : ValueConverter<TEnum, string> where TEnum : struct, Enum
{
    private static readonly Dictionary<TEnum, string> ToDb =
        Enum.GetValues<TEnum>().ToDictionary(v => v, v => ModelConventions.ToSnakeCase(v.ToString()));

    private static readonly Dictionary<string, TEnum> FromDb =
        ToDb.ToDictionary(kv => kv.Value, kv => kv.Key);

    public SnakeCaseEnumConverter() : base(v => ToDb[v], v => FromDb[v]) { }
}
