using System.Net.Http.Json;

namespace OneLoaf.IntegrationTests.Infrastructure;

/// <summary>Ids from <c>db/seed/test_data.sql</c> used by the acceptance tests (business analysis values).</summary>
public static class Seed
{
    public static readonly Guid Anna = new("00000000-0000-4000-a001-0000000000a1");
    public static readonly Guid Boris = new("00000000-0000-4000-a001-0000000000a2");

    /// <summary>Anna's "White homemade loaf": 1 loaf, flour 500 g, starter 100 g, stages 14-16 h.</summary>
    public static readonly Guid R1 = new("00000000-0000-4000-2001-000000000001");

    /// <summary>Variant of R1 ("White loaf with rye").</summary>
    public static readonly Guid R2 = new("00000000-0000-4000-2001-000000000002");

    /// <summary>Variant of R2 (removes an inherited line).</summary>
    public static readonly Guid R3 = new("00000000-0000-4000-2001-000000000003");

    /// <summary>Boris's recipe with an ingredient without a price; Boris has no oven or bowl settings.</summary>
    public static readonly Guid R5 = new("00000000-0000-4000-2001-000000000005");

    /// <summary>Anna's flour "Bread flour T550" (12.5 % protein).</summary>
    public static readonly Guid AnnaFlour = new("00000000-0000-4000-f001-000000000001");

    /// <summary>Anna's ingredient "Bread flour" (used in R1, prices changed on 1 October).</summary>
    public static readonly Guid AnnaBreadFlourIngredient = new("00000000-0000-4000-1001-000000000001");

    /// <summary>Reads JSON with the API's serializer settings.</summary>
    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(CoreApiFactory.Json, TestContext.Current.CancellationToken))!;
}
