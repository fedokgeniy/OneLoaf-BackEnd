using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using OneLoaf.Assistant.Application.Claude;
using OneLoaf.Assistant.Application.Tools;
using OneLoaf.Domain;
using OneLoaf.IntegrationTests.Infrastructure;
using OneLoaf.Messaging.Contracts;
using OneLoaf.SharedKernel.Exceptions;

namespace OneLoaf.IntegrationTests.Docs;

/// <summary>
/// Regenerates <c>docs/api</c> from the running services: the OpenAPI documents, request/response examples recorded
/// from a realistic scenario that calls every endpoint (typical and edge cases), the Markdown reference built from both,
/// the JSON schemas and the event contracts. Runs only with <c>ONELOAF_EXPORT_DOCS=1</c>:
/// <code>ONELOAF_EXPORT_DOCS=1 dotnet test tests/OneLoaf.IntegrationTests --filter FullyQualifiedName~ApiDocsGenerator</code>
/// </summary>
[Collection(ApiTestGroup.Name)]
public sealed class ApiDocsGenerator(ApiFixture fixture)
{
    public const string ExportVariable = "ONELOAF_EXPORT_DOCS";
    private const string Password = "Rye-Bread-2026!";
    private static readonly Guid Admin = new("00000000-0000-4000-a001-0000000000a5");
    private static readonly Guid Missing = new("00000000-0000-4000-9999-000000000000");

    private static readonly (string Name, string Title, string BaseUrls)[] Services =
    [
        ("core-api", "Core API", "`https://<domain>/core` (production behind Caddy), `http://localhost:5080` (local)"),
        ("assistant", "AI Assistant", "`https://<domain>/assistant` (production), `http://localhost:5081` (local)"),
        ("notification", "Notification", "`https://<domain>/notify` (production), `http://localhost:5082` (local)"),
    ];

    private readonly ExampleStore _store = new();

    [Fact]
    public async Task Generate()
    {
        Assert.SkipUnless(Environment.GetEnvironmentVariable(ExportVariable) == "1", $"Set {ExportVariable}=1 to regenerate docs/api.");
        var ct = TestContext.Current.CancellationToken;
        var docs = Path.Combine(DocsPaths.Root, "docs", "api");

        var workspace = await CoreApiScenarioAsync();
        await AssistantScenarioAsync(workspace);
        await NotificationScenarioAsync(workspace.Token);

        var assistantForSpec = fixture.CreateAssistant(new ScriptedClaude());
        var specClients = new Dictionary<string, HttpClient>(StringComparer.Ordinal)
        {
            ["core-api"] = fixture.Api.CreateClient(),
            ["assistant"] = assistantForSpec.CreateClient(),
            ["notification"] = fixture.Notification.CreateClient(),
        };

        var report = new StringBuilder();
        foreach (var (name, title, baseUrls) in Services)
        {
            using var client = specClients[name];
            var spec = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json", ct))!;
            await DocsPaths.WriteJsonAsync(Path.Combine(docs, "reference", $"{name}.openapi.json"), spec, ct);
            await DocsPaths.WriteJsonAsync(Path.Combine(docs, "schemas", $"{name}.schemas.json"), spec["components"]!["schemas"]!.DeepClone(), ct);

            var examples = _store.For(name);
            var markdown = new MarkdownReference(name, title, baseUrls, spec, examples);
            await DocsPaths.WriteTextAsync(Path.Combine(docs, "reference", $"{name}.md"), markdown.Render(), ct);
            foreach (var (operationId, exchanges) in examples)
            {
                await DocsPaths.WriteJsonAsync(Path.Combine(docs, "examples", name, $"{operationId}.json"),
                    JsonSerializer.SerializeToNode(exchanges, DocsPaths.ExampleJson)!, ct);
            }

            await DocsPaths.WriteTextAsync(Path.Combine(docs, "examples", $"{name}.http"), HttpFile(name, baseUrls, spec, examples), ct);
            report.AppendLine(CultureInfo.InvariantCulture, $"{name}: {markdown.Synthesized} operations without a recorded example");
        }

        await WriteEventSchemasAsync(Path.Combine(docs, "schemas", "events"), ct);
        TestContext.Current.SendDiagnosticMessage(report.ToString());
    }

    private async Task<Workspace> CoreApiScenarioAsync()
    {
        using var api = new DocClient("core-api", fixture.Api.Server.CreateHandler(), fixture.Api.Server.BaseAddress, _store);
        var email = $"docs.baker.{Guid.NewGuid():N}"[..20] + "@example.com";

        // Auth
        await api.CallAsync("Register", "typical", HttpMethod.Post, "/api/v1/auth/register",
            new { email, password = Password, displayName = "Docs Baker" });
        await api.CallAsync("Register", "e-mail already registered", HttpMethod.Post, "/api/v1/auth/register",
            new { email, password = Password, displayName = "Docs Baker" });
        await api.CallAsync("Register", "invalid fields", HttpMethod.Post, "/api/v1/auth/register",
            new { email = "not-an-email", password = "short", displayName = "" });
        var login = await api.CallAsync("Login", "typical", HttpMethod.Post, "/api/v1/auth/login", new { email, password = Password });
        await api.CallAsync("Login", "wrong password", HttpMethod.Post, "/api/v1/auth/login", new { email, password = "Wrong-Password-1" });
        var token = login!["accessToken"]!.GetValue<string>();
        await api.CallAsync("GetMe", "without token", HttpMethod.Get, "/api/v1/me");
        api.As(token);
        await api.CallAsync("GetMe", "typical", HttpMethod.Get, "/api/v1/me");

        // Reference data
        await api.CallAsync("GetUnits", "typical", HttpMethod.Get, "/api/v1/reference/units");
        await api.CallAsync("GetCurrencies", "typical", HttpMethod.Get, "/api/v1/reference/currencies");
        await api.CallAsync("GetStageTypes", "typical", HttpMethod.Get, "/api/v1/reference/stage-types");
        await api.CallAsync("GetIngredientCategories", "typical", HttpMethod.Get, "/api/v1/reference/ingredient-categories");
        await api.CallAsync("GetFlourTypes", "typical", HttpMethod.Get, "/api/v1/reference/flour-types");

        // Kitchen
        await api.CallAsync("GetKitchenSettings", "defaults after registration", HttpMethod.Get, "/api/v1/kitchen-settings");
        var kitchen = new
        {
            ovenPowerKw = 2.5m, ovenCapacityLoaves = 4, mixerBowlCapacityKg = 4.0m, electricityTariffPerKwh = 0.25m,
            packagingCostPerLoaf = 0.15m, lossRatePercent = 10m, laborRatePerHour = 12m, currency = "EUR", timeZone = "Europe/Vilnius",
        };
        await api.CallAsync("UpdateKitchenSettings", "typical", HttpMethod.Put, "/api/v1/kitchen-settings", kitchen);
        await api.CallAsync("UpdateKitchenSettings", "unknown time zone", HttpMethod.Put, "/api/v1/kitchen-settings",
            kitchen with { timeZone = "Mars/Olympus" });

        // Flours
        var flour = await api.CallAsync("CreateFlour", "typical", HttpMethod.Post, "/api/v1/flours",
            new { name = "Bread flour T550", flourTypeCode = "wheat", proteinPercent = 12.5m, grind = "fine", notes = (string?)null });
        var flourId = Id(flour);
        await api.CallAsync("CreateFlour", "name already used", HttpMethod.Post, "/api/v1/flours",
            new { name = "Bread flour T550", flourTypeCode = "wheat", proteinPercent = 12.5m, grind = "fine", notes = (string?)null });
        await api.CallAsync("ListFlours", "typical", HttpMethod.Get, "/api/v1/flours?page=1&pageSize=20");
        await api.CallAsync("GetFlour", "typical", HttpMethod.Get, $"/api/v1/flours/{flourId}");
        await api.CallAsync("GetFlour", "not found or another user's", HttpMethod.Get, $"/api/v1/flours/{Missing}");
        await api.CallAsync("UpdateFlour", "typical", HttpMethod.Put, $"/api/v1/flours/{flourId}",
            new { name = "Bread flour T550", flourTypeCode = "wheat", proteinPercent = 12.8m, grind = "fine", notes = "Local mill" });
        var spareFlour = await api.CallAsync("CreateFlour", "second flour", HttpMethod.Post, "/api/v1/flours",
            new { name = "Spare spelt", flourTypeCode = "spelt", proteinPercent = 11m, grind = (string?)null, notes = (string?)null });
        await api.CallAsync("DeleteFlour", "typical", HttpMethod.Delete, $"/api/v1/flours/{Id(spareFlour)}");

        // Ingredients and prices
        var flourIngredient = Id(await api.CallAsync("CreateIngredient", "flour linked to a flour", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Bread flour", categoryCode = "flour", unitCode = "g", flourId }));
        var water = Id(await api.CallAsync("CreateIngredient", "typical", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Water", categoryCode = "water", unitCode = "g", flourId = (Guid?)null }));
        var salt = Id(await api.CallAsync("CreateIngredient", "salt", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Salt", categoryCode = "salt", unitCode = "g", flourId = (Guid?)null }));
        var starterIngredient = Id(await api.CallAsync("CreateIngredient", "starter", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Rye starter", categoryCode = "starter", unitCode = "g", flourId = (Guid?)null }));
        await api.CallAsync("CreateIngredient", "unknown unit", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Honey", categoryCode = "sweetener", unitCode = "spoon", flourId = (Guid?)null });
        await api.CallAsync("ListIngredients", "filter by category", HttpMethod.Get, "/api/v1/ingredients?category=flour&page=1&pageSize=20");
        await api.CallAsync("GetIngredient", "typical", HttpMethod.Get, $"/api/v1/ingredients/{salt}");
        await api.CallAsync("UpdateIngredient", "typical", HttpMethod.Put, $"/api/v1/ingredients/{salt}",
            new { name = "Sea salt", categoryCode = "salt", unitCode = "g", flourId = (Guid?)null });
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var priceFrom = today.AddDays(-30).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await api.CallAsync("AddIngredientPrice", "typical", HttpMethod.Post, $"/api/v1/ingredients/{flourIngredient}/prices",
            new { price = 1.20m, perQuantity = 1000m, currency = "EUR", effectiveFrom = priceFrom });
        await api.CallAsync("AddIngredientPrice", "price change", HttpMethod.Post, $"/api/v1/ingredients/{flourIngredient}/prices",
            new { price = 1.35m, perQuantity = 1000m, currency = "EUR", effectiveFrom = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        foreach (var (ingredient, price) in new[] { (water, 0.01m), (salt, 0.80m), (starterIngredient, 0.50m) })
        {
            await api.CallAsync("AddIngredientPrice", "other ingredient", HttpMethod.Post, $"/api/v1/ingredients/{ingredient}/prices",
                new { price, perQuantity = 1000m, currency = "EUR", effectiveFrom = priceFrom });
        }

        await api.CallAsync("GetIngredientPrices", "history with change percent", HttpMethod.Get, $"/api/v1/ingredients/{flourIngredient}/prices");
        var sesame = Id(await api.CallAsync("CreateIngredient", "unused ingredient", HttpMethod.Post, "/api/v1/ingredients",
            new { name = "Sesame", categoryCode = "add_in", unitCode = "g", flourId = (Guid?)null }));
        await api.CallAsync("DeleteIngredient", "typical", HttpMethod.Delete, $"/api/v1/ingredients/{sesame}");

        // Recipes
        var recipeBody = new
        {
            title = "Country sourdough", description = "Open crumb, crisp crust.", yieldLoaves = 1, loafWeightG = 900m, notes = (string?)null,
            ingredients = new object[]
            {
                new { ingredientId = flourIngredient, quantity = 500m },
                new { ingredientId = water, quantity = 350m },
                new { ingredientId = salt, quantity = 10m },
                new { ingredientId = starterIngredient, quantity = 100m },
            },
            stages = new object[]
            {
                new { name = "Mix", stageTypeCode = "mix", minDurationMinutes = 15, maxDurationMinutes = 20, activeMinutes = 15 },
                new { name = "Bulk fermentation", stageTypeCode = "bulk_fermentation", minDurationMinutes = 240, maxDurationMinutes = 300, temperatureC = 24m },
                new { name = "Shape", stageTypeCode = "shape", minDurationMinutes = 10, maxDurationMinutes = 15, activeMinutes = 10 },
                new { name = "Cold proof", stageTypeCode = "proof", minDurationMinutes = 600, maxDurationMinutes = 720, proofMode = "cold", temperatureC = 4m },
                new { name = "Bake", stageTypeCode = "bake", minDurationMinutes = 40, maxDurationMinutes = 45, temperatureC = 240m },
            },
        };
        var recipe = Id(await api.CallAsync("CreateRecipe", "typical", HttpMethod.Post, "/api/v1/recipes", recipeBody));
        await api.CallAsync("CreateRecipe", "minimum longer than maximum", HttpMethod.Post, "/api/v1/recipes",
            new
            {
                title = "Broken", description = (string?)null, yieldLoaves = 1, loafWeightG = (decimal?)null, notes = (string?)null,
                ingredients = new object[] { new { ingredientId = water, quantity = 100m } },
                stages = new object[] { new { name = "Mix", stageTypeCode = "mix", minDurationMinutes = 30, maxDurationMinutes = 10 } },
            });
        await api.CallAsync("DeleteIngredient", "used in a recipe", HttpMethod.Delete, $"/api/v1/ingredients/{flourIngredient}");
        await api.CallAsync("ListRecipes", "typical", HttpMethod.Get, "/api/v1/recipes?search=country&page=1&pageSize=20");
        await api.CallAsync("ListRecipes", "by flour protein", HttpMethod.Get, "/api/v1/recipes?minProteinPercent=12");
        await api.CallAsync("GetRecipe", "typical", HttpMethod.Get, $"/api/v1/recipes/{recipe}");
        await api.CallAsync("UpdateRecipe", "typical", HttpMethod.Put, $"/api/v1/recipes/{recipe}", recipeBody with { notes = "Feed the starter the evening before." });
        var variant = Id(await api.CallAsync("CreateVariant", "typical", HttpMethod.Post, $"/api/v1/recipes/{recipe}/variants",
            new { title = "Country sourdough, longer bulk" }));
        var variantBody = recipeBody with
        {
            title = "Country sourdough, longer bulk",
            stages = recipeBody.stages.Select((s, i) => i == 1
                ? (object)new { name = "Bulk fermentation", stageTypeCode = "bulk_fermentation", minDurationMinutes = 300, maxDurationMinutes = 360, temperatureC = 22m }
                : s).ToArray(),
        };
        await api.CallAsync("UpdateRecipe", "variant (only differences are stored)", HttpMethod.Put, $"/api/v1/recipes/{variant}", variantBody);
        await api.CallAsync("CompareVariant", "typical", HttpMethod.Get, $"/api/v1/recipes/{variant}/comparison");
        await api.CallAsync("CompareVariant", "not a variant", HttpMethod.Get, $"/api/v1/recipes/{recipe}/comparison");
        await api.CallAsync("ScaleRecipe", "typical", HttpMethod.Get, $"/api/v1/recipes/{recipe}/scaled?loaves=3");
        await api.CallAsync("GetRecipeCost", "typical", HttpMethod.Get, $"/api/v1/recipes/{recipe}/cost?loaves=3");
        await api.CallAsync("GetRecipeCost", "loaves out of range", HttpMethod.Get, $"/api/v1/recipes/{recipe}/cost?loaves=0");
        var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(2).AddHours(6), TimeSpan.Zero).ToOffset(TimeSpan.FromHours(2));
        var startText = Uri.EscapeDataString(start.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture));
        await api.CallAsync("GetRecipeTimeline", "typical", HttpMethod.Get, $"/api/v1/recipes/{recipe}/timeline?start={startText}");
        await api.CallAsync("GetRecipeStartWindow", "typical", HttpMethod.Get,
            $"/api/v1/recipes/{recipe}/start-window?readyAt={Uri.EscapeDataString(start.AddDays(1).ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture))}");
        await api.CallAsync("GetProductionFigures", "typical", HttpMethod.Get, $"/api/v1/recipes/{recipe}/production-figures?loaves=3");
        await api.CallAsync("PlanOvenLoading", "typical", HttpMethod.Post, "/api/v1/calculations/oven-loading",
            new { items = new[] { new { recipeId = recipe, loaves = 3 }, new { recipeId = variant, loaves = 2 } } });
        await api.CallAsync("GetWarnings", "typical", HttpMethod.Get, "/api/v1/warnings");
        await api.CallAsync("ArchiveRecipe", "typical", HttpMethod.Post, $"/api/v1/recipes/{variant}/archive");
        await api.CallAsync("UnarchiveRecipe", "typical", HttpMethod.Post, $"/api/v1/recipes/{variant}/unarchive");
        await api.CallAsync("DeleteRecipe", "has variants", HttpMethod.Delete, $"/api/v1/recipes/{recipe}");

        // Starters
        var starter = Id(await api.CallAsync("CreateStarter", "typical", HttpMethod.Post, "/api/v1/starters",
            new { name = "Rye mother", hydrationPercent = 100m, feedingMode = "regular", feedingIntervalHours = 24, flourId, riseHours = 8m }));
        await api.CallAsync("ListStarters", "typical", HttpMethod.Get, "/api/v1/starters");
        await api.CallAsync("GetStarter", "typical", HttpMethod.Get, $"/api/v1/starters/{starter}");
        await api.CallAsync("UpdateStarter", "typical", HttpMethod.Put, $"/api/v1/starters/{starter}",
            new { name = "Rye mother", hydrationPercent = 100m, feedingMode = "regular", feedingIntervalHours = 12, flourId, riseHours = 8m, notes = "Kitchen counter" });
        await api.CallAsync("AddFeeding", "typical", HttpMethod.Post, $"/api/v1/starters/{starter}/feedings",
            new { fedAt = DateTimeOffset.UtcNow.AddHours(-1), flourG = 50m, waterG = 50m, starterKeptG = 20m, temperatureC = 24m });
        await api.CallAsync("ListFeedings", "typical", HttpMethod.Get, $"/api/v1/starters/{starter}/feedings");
        var spareStarter = Id(await api.CallAsync("CreateStarter", "fridge starter", HttpMethod.Post, "/api/v1/starters",
            new { name = "Fridge levain", hydrationPercent = 80m, feedingMode = "fridge", feedingIntervalHours = 168 }));
        await api.CallAsync("DeleteStarter", "typical", HttpMethod.Delete, $"/api/v1/starters/{spareStarter}");

        // Batches: one is baked through to the end, one shows the stage controls and is cancelled
        var batchBody = new
        {
            name = "Weekend bake", plannedStartAt = start, source = "manual",
            items = new[] { new { recipeId = recipe, loaves = 3, starterId = (string?)starter, starterReminderMinutes = (int?)60 } },
        };
        await api.CallAsync("PreviewBatch", "typical", HttpMethod.Post, "/api/v1/batches/preview", batchBody);
        var batch = await api.CallAsync("CreateBatch", "typical", HttpMethod.Post, "/api/v1/batches", batchBody);
        var batchId = Id(batch);
        await api.CallAsync("CreateBatch", "no items", HttpMethod.Post, "/api/v1/batches", batchBody with { items = [] });
        await api.CallAsync("ListBatches", "by status", HttpMethod.Get, "/api/v1/batches?status=planned");
        await api.CallAsync("ListBatches", "unknown status", HttpMethod.Get, "/api/v1/batches?status=baking");
        await api.CallAsync("ListBatches", "calendar range", HttpMethod.Get,
            $"/api/v1/batches?from={Uri.EscapeDataString(start.AddDays(-1).ToString("O", CultureInfo.InvariantCulture))}&page=1&pageSize=20");
        await api.CallAsync("GetBatch", "typical", HttpMethod.Get, $"/api/v1/batches/{batchId}");
        await api.CallAsync("GetKneadingPlan", "typical", HttpMethod.Get, $"/api/v1/batches/{batchId}/kneading-plan");
        await api.CallAsync("ReplanBatch", "move the start", HttpMethod.Put, $"/api/v1/batches/{batchId}", batchBody with { plannedStartAt = start.AddHours(1) });

        var current = await api.CallAsync("GetBatch", "after replanning", HttpMethod.Get, $"/api/v1/batches/{batchId}");
        var stages = current!["items"]![0]!["stages"]!.AsArray().Select(s => Id(s)).ToList();
        for (var i = 0; i < stages.Count; i++)
        {
            await api.CallAsync("StartStage", i == 0 ? "typical" : "next stage", HttpMethod.Post, $"/api/v1/batches/{batchId}/stages/{stages[i]}/start",
                new { actualTemperatureC = (decimal?)null, note = (string?)null });
            await api.CallAsync("CompleteStage", i == 0 ? "typical" : "next stage", HttpMethod.Post, $"/api/v1/batches/{batchId}/stages/{stages[i]}/complete",
                new { actualTemperatureC = (decimal?)24.5m, note = i == 1 ? "Doubled in size" : null });
        }

        var completed = await api.CallAsync("CompleteBatch", "typical", HttpMethod.Post, $"/api/v1/batches/{batchId}/complete");
        await api.CallAsync("CompleteBatch", "already completed", HttpMethod.Post, $"/api/v1/batches/{batchId}/complete");

        await api.CallAsync("SaveBatchAsVariant", "typical", HttpMethod.Post, $"/api/v1/batches/{batchId}/save-as-variant",
            new { itemId = (string?)null, title = "Country sourdough, as baked" });
        var untouched = Id(await api.CallAsync("CreateBatch", "batch that follows the recipe", HttpMethod.Post, "/api/v1/batches",
            batchBody with { name = "Untouched bake", plannedStartAt = start.AddDays(5) }));
        await api.CallAsync("SaveBatchAsVariant", "batch followed the recipe", HttpMethod.Post, $"/api/v1/batches/{untouched}/save-as-variant",
            new { itemId = (string?)null, title = (string?)null });
        await api.CallAsync("CancelBatch", "untouched batch", HttpMethod.Post, $"/api/v1/batches/{untouched}/cancel");
        var second = Id(await api.CallAsync("CreateBatch", "second batch", HttpMethod.Post, "/api/v1/batches",
            batchBody with { name = "Weekday bake", plannedStartAt = start.AddDays(3) }));
        var secondStages = (await api.CallAsync("GetBatch", "second batch", HttpMethod.Get, $"/api/v1/batches/{second}"))!["items"]![0]!["stages"]!
            .AsArray().Select(s => Id(s)).ToList();
        await api.CallAsync("ChangeStageDurations", "typical", HttpMethod.Patch, $"/api/v1/batches/{second}/stages/{secondStages[1]}",
            new { plannedMinMinutes = 270, plannedMaxMinutes = 330 });
        await api.CallAsync("ChangeStageDurations", "minimum longer than maximum", HttpMethod.Patch, $"/api/v1/batches/{second}/stages/{secondStages[1]}",
            new { plannedMinMinutes = 400, plannedMaxMinutes = 300 });
        await api.CallAsync("ResetStage", "typical", HttpMethod.Post, $"/api/v1/batches/{second}/stages/{secondStages[1]}/reset");
        await api.CallAsync("StartStage", "first stage of the second batch", HttpMethod.Post, $"/api/v1/batches/{second}/stages/{secondStages[0]}/start",
            new { actualTemperatureC = (decimal?)null, note = (string?)null });
        await api.CallAsync("ExtendStage", "typical", HttpMethod.Post, $"/api/v1/batches/{second}/stages/{secondStages[0]}/extend", new { minutes = 30 });
        await api.CallAsync("ResetStage", "stage already running", HttpMethod.Post, $"/api/v1/batches/{second}/stages/{secondStages[0]}/reset");
        await api.CallAsync("SkipStage", "typical", HttpMethod.Post, $"/api/v1/batches/{second}/stages/{secondStages[2]}/skip",
            new { actualTemperatureC = (decimal?)null, note = "Baked without shaping, in a tin" });
        await api.CallAsync("CancelBatch", "typical", HttpMethod.Post, $"/api/v1/batches/{second}/cancel");
        await api.CallAsync("CancelBatch", "not found or another user's", HttpMethod.Post, $"/api/v1/batches/{Missing}/cancel");

        // History
        var bakeRecord = completed?["bakeRecordId"]?.GetValue<string>();
        await api.CallAsync("ListBakeRecords", "typical", HttpMethod.Get, "/api/v1/bake-records?page=1&pageSize=20");
        if (bakeRecord is not null)
        {
            await api.CallAsync("GetBakeRecord", "typical", HttpMethod.Get, $"/api/v1/bake-records/{bakeRecord}");
            await api.CallAsync("RateBake", "typical", HttpMethod.Patch, $"/api/v1/bake-records/{bakeRecord}",
                new { rating = (short)4, comment = "Good oven spring, crumb a bit tight." });
            await api.CallAsync("RateBake", "rating out of range", HttpMethod.Patch, $"/api/v1/bake-records/{bakeRecord}", new { rating = (short)9, comment = (string?)null });
            await api.CallAsync("GetStageLog", "typical", HttpMethod.Get, $"/api/v1/bake-records/{bakeRecord}/stage-log");
            await api.CallAsync("SaveBakeAnalysis", "typical", HttpMethod.Put, $"/api/v1/bake-records/{bakeRecord}/analysis",
                new { analysis = "Bulk fermentation ended early; give the dough more time next bake." });
        }

        if (bakeRecord is not null)
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0];
            var photo = Id(await api.UploadAsync("UploadBakePhoto", "typical", $"/api/v1/bake-records/{bakeRecord}/photos", "file", "crumb.png", "image/png", png));
            await api.UploadAsync("UploadBakePhoto", "not an image", $"/api/v1/bake-records/{bakeRecord}/photos", "file", "notes.png", "image/png", "plain text"u8.ToArray());
            await api.CallAsync("ListBakePhotos", "typical", HttpMethod.Get, $"/api/v1/bake-records/{bakeRecord}/photos");
            await api.CallAsync("GetBakePhoto", "typical", HttpMethod.Get, $"/api/v1/bake-records/{bakeRecord}/photos/{photo}");
            await api.CallAsync("GetBakeRecord", "with a photo", HttpMethod.Get, $"/api/v1/bake-records/{bakeRecord}");
            var spare = Id(await api.UploadAsync("UploadBakePhoto", "second photo", $"/api/v1/bake-records/{bakeRecord}/photos", "file", "crumb-2.png", "image/png", png));
            await api.CallAsync("DeleteBakePhoto", "typical", HttpMethod.Delete, $"/api/v1/bake-records/{bakeRecord}/photos/{spare}");
            await api.CallAsync("DeleteBakePhoto", "not found or another user's", HttpMethod.Delete, $"/api/v1/bake-records/{bakeRecord}/photos/{spare}");
        }

        await api.CallAsync("GetBakeStatistics", "typical", HttpMethod.Get, "/api/v1/bake-records/statistics");
        await api.CallAsync("ExportRecipes", "typical", HttpMethod.Get, "/api/v1/export/recipes.csv");
        await api.CallAsync("ExportBakeRecords", "typical", HttpMethod.Get, "/api/v1/export/bake-records.csv");

        // Library and moderation
        var publication = Id(await api.CallAsync("PublishRecipe", "typical", HttpMethod.Post, $"/api/v1/recipes/{recipe}/publish"), "publishedRecipeId");
        api.As(null);
        await api.CallAsync("SearchLibrary", "anonymous search", HttpMethod.Get, "/api/v1/library?search=sourdough&page=1&pageSize=20");
        await api.CallAsync("GetPublication", "anonymous", HttpMethod.Get, $"/api/v1/library/{publication}");
        await api.CallAsync("GetPublication", "not found", HttpMethod.Get, $"/api/v1/library/{Missing}");
        api.As(fixture.TokenFor(Seed.Boris));
        var comment = Id(await api.CallAsync("AddComment", "typical", HttpMethod.Post, $"/api/v1/library/{publication}/comments",
            new { body = "Baked it on Sunday, lovely crumb!" }));
        await api.CallAsync("AddComment", "empty body", HttpMethod.Post, $"/api/v1/library/{publication}/comments", new { body = " " });
        await api.CallAsync("SetReaction", "typical", HttpMethod.Put, $"/api/v1/library/{publication}/reactions/me", new { reaction = "want_to_bake" });
        await api.CallAsync("ListComments", "typical", HttpMethod.Get, $"/api/v1/library/{publication}/comments?page=1&pageSize=20");
        await api.CallAsync("CopyPublication", "typical", HttpMethod.Post, $"/api/v1/library/{publication}/copy");
        await api.CallAsync("RemoveReaction", "typical", HttpMethod.Delete, $"/api/v1/library/{publication}/reactions/me");
        var report = Id(await api.CallAsync("ReportContent", "a comment", HttpMethod.Post, $"/api/v1/library/{publication}/reports",
            new { reason = "Off-topic advertising link", commentId = comment }), "reportId");
        await api.CallAsync("ListReports", "not an administrator", HttpMethod.Get, "/api/v1/admin/reports");
        await api.CallAsync("UnpublishRecipe", "not the author", HttpMethod.Post, $"/api/v1/library/{publication}/unpublish");
        await api.CallAsync("RemoveComment", "typical", HttpMethod.Delete, $"/api/v1/library/{publication}/comments/{comment}");
        api.As(fixture.TokenFor(Admin, UserRole.Admin));
        await api.CallAsync("ListReports", "typical", HttpMethod.Get, "/api/v1/admin/reports?status=open&page=1&pageSize=20");
        await api.CallAsync("ResolveReport", "reject", HttpMethod.Post, $"/api/v1/admin/reports/{report}/resolve",
            new { action = "reject", reason = "The comment was already removed by its author." });
        await api.CallAsync("ResolveReport", "already decided", HttpMethod.Post, $"/api/v1/admin/reports/{report}/resolve",
            new { action = "remove", reason = "Advertising is not allowed." });
        api.As(token);
        await api.CallAsync("UnpublishRecipe", "typical", HttpMethod.Post, $"/api/v1/library/{publication}/unpublish");
        var second2 = Id(await api.CallAsync("PublishRecipe", "publish again (new snapshot)", HttpMethod.Post, $"/api/v1/recipes/{recipe}/publish"), "publishedRecipeId");
        api.As(fixture.TokenFor(Admin, UserRole.Admin));
        await api.CallAsync("RemovePublication", "typical", HttpMethod.Post, $"/api/v1/admin/library/{second2}/remove",
            new { reason = "Duplicate of an earlier publication." });

        // Account (a separate throw-away account, so the workspace above stays for the other services)
        api.As(null);
        var throwaway = $"leaving.{Guid.NewGuid():N}"[..16] + "@example.com";
        await api.CallAsync("Register", "second account", HttpMethod.Post, "/api/v1/auth/register",
            new { email = throwaway, password = Password, displayName = "Leaving Baker" });
        var leaving = (await api.CallAsync("Login", "second account", HttpMethod.Post, "/api/v1/auth/login", new { email = throwaway, password = Password }))!;
        api.As(leaving["accessToken"]!.GetValue<string>());
        await api.CallAsync("DeleteMe", "wrong password", HttpMethod.Delete, "/api/v1/me", new { password = "Not-My-Password-1" });
        await api.CallAsync("Logout", "typical", HttpMethod.Post, "/api/v1/auth/logout");
        await api.CallAsync("DeleteMe", "typical", HttpMethod.Delete, "/api/v1/me", new { password = Password });
        return new Workspace(token, recipe, bakeRecord, start);
    }

    private async Task AssistantScenarioAsync(Workspace workspace)
    {
        string? quotedCost = null;
        var claude = new ScriptedClaude(
            _ => ToolUse(ToolNames.FindRecipes, new { query = "Country sourdough" }),
            _ => ToolUse(ToolNames.ProposeBatch, new
            {
                name = "Saturday bake",
                plannedStartAt = workspace.Start.AddDays(7).ToString("O", CultureInfo.InvariantCulture),
                items = new[] { new { recipeId = workspace.RecipeId, loaves = 2 } },
            }),
            request =>
            {
                using var proposal = JsonDocument.Parse(ScriptedClaude.LastToolResult(request));
                quotedCost = proposal.RootElement.GetProperty("plan").GetProperty("totalCost") is { ValueKind: JsonValueKind.Number } cost
                    ? cost.GetDecimal().ToString(CultureInfo.InvariantCulture)
                    : null;
                return Text(quotedCost is null
                    ? "I am an AI assistant. I prepared the Saturday bake of Country sourdough. Please check the plan and confirm it."
                    : $"I am an AI assistant. I prepared the Saturday bake of Country sourdough; it costs {quotedCost} EUR. Please confirm it.");
            },
            _ => ToolUse(ToolNames.ProposeBatch, new
            {
                name = "Sunday bake",
                plannedStartAt = workspace.Start.AddDays(8).ToString("O", CultureInfo.InvariantCulture),
                items = new[] { new { recipeId = workspace.RecipeId, loaves = 1 } },
            }),
            _ => Text("I prepared the Sunday bake as well. Confirm or decline it."),
            _ => Text("The rating is good. The tight crumb suggests the bulk fermentation was a little short for the room temperature; " +
                      "let the dough rise longer before shaping next time."));
        var assistant = fixture.CreateAssistant(claude);
        using var chat = new DocClient("assistant", assistant.Server.CreateHandler(), assistant.Server.BaseAddress, _store);

        await chat.CallAsync("ListConversations", "without token", HttpMethod.Get, "/api/v1/conversations");
        chat.As(workspace.Token);
        var conversation = Id(await chat.CallAsync("CreateConversation", "typical", HttpMethod.Post, "/api/v1/conversations", new { title = (string?)null }));
        var reply = await chat.CallAsync("SendMessage", "plans a batch with tools (proposal to confirm)", HttpMethod.Post,
            $"/api/v1/conversations/{conversation}/messages", new { content = "Plan 2 loaves of my Country sourdough for next Saturday morning." });
        await chat.CallAsync("SendMessage", "empty message", HttpMethod.Post, $"/api/v1/conversations/{conversation}/messages", new { content = "" });
        var action = reply!["pendingAction"]!["actionId"]!.GetValue<string>();
        await chat.CallAsync("ConfirmAction", "typical", HttpMethod.Post, $"/api/v1/conversations/{conversation}/actions/{action}/confirm");
        await chat.CallAsync("ConfirmAction", "already used", HttpMethod.Post, $"/api/v1/conversations/{conversation}/actions/{action}/confirm");
        var second = await chat.CallAsync("SendMessage", "second proposal", HttpMethod.Post, $"/api/v1/conversations/{conversation}/messages",
            new { content = "And one loaf on Sunday." });
        var declined = second!["pendingAction"]!["actionId"]!.GetValue<string>();
        await chat.CallAsync("DeclineAction", "typical", HttpMethod.Post, $"/api/v1/conversations/{conversation}/actions/{declined}/decline");
        await chat.CallAsync("ListMessages", "typical", HttpMethod.Get, $"/api/v1/conversations/{conversation}/messages");
        await chat.CallAsync("ListMessages", "audit with tool calls", HttpMethod.Get, $"/api/v1/conversations/{conversation}/messages?includeTools=true");
        await chat.CallAsync("ListConversations", "typical", HttpMethod.Get, "/api/v1/conversations");
        if (workspace.BakeRecordId is { } record)
        {
            await chat.CallAsync("AnalyseBake", "typical", HttpMethod.Post, $"/api/v1/bake-records/{record}/analysis");
        }

        await chat.CallAsync("AnalyseBake", "not found or another user's", HttpMethod.Post, $"/api/v1/bake-records/{Missing}/analysis");
        await chat.CallAsync("GetMyUsage", "typical", HttpMethod.Get, "/api/v1/usage/me");
        var spare = Id(await chat.CallAsync("CreateConversation", "with a title", HttpMethod.Post, "/api/v1/conversations", new { title = "Rye questions" }));
        await chat.CallAsync("DeleteConversation", "typical", HttpMethod.Delete, $"/api/v1/conversations/{spare}");
        await chat.CallAsync("ListMessages", "deleted conversation", HttpMethod.Get, $"/api/v1/conversations/{spare}/messages");

        var down = fixture.CreateAssistant(new ScriptedClaude(_ => throw new ExternalServiceUnavailableException(
            "The assistant is temporarily unavailable. Use the planning form or try again later.")));
        using var unavailable = new DocClient("assistant", down.Server.CreateHandler(), down.Server.BaseAddress, _store);
        unavailable.As(workspace.Token);
        var other = Id(await unavailable.CallAsync("CreateConversation", "for the outage example", HttpMethod.Post, "/api/v1/conversations", new { title = (string?)null }));
        await unavailable.CallAsync("SendMessage", "Claude unavailable", HttpMethod.Post, $"/api/v1/conversations/{other}/messages",
            new { content = "When should I start for bread at 8 am?" });
    }

    private async Task NotificationScenarioAsync(string token)
    {
        var notification = fixture.Notification;
        using var notify = new DocClient("notification", notification.Server.CreateHandler(), notification.Server.BaseAddress, _store);
        await notify.CallAsync("GetVapidPublicKey", "typical", HttpMethod.Get, "/api/v1/push/vapid-public-key");
        await notify.CallAsync("ListReminders", "without token", HttpMethod.Get, "/api/v1/reminders");
        notify.As(token);
        var device = new
        {
            endpoint = $"https://fcm.googleapis.com/fcm/send/docs-{Guid.NewGuid():N}",
            keys = new { p256dh = "BNcRdreALRFXTkOOUHK1EtK2wtaz5Ry4YfYCA_0QTpQtUbVlUls0VJXg7A8u-Ts1XbjhazAkj7I99e8QcYP7DkM", auth = "tBHItJI5svbpez7KI4CCXg" },
        };
        var subscription = Id(await notify.CallAsync("Subscribe", "new device", HttpMethod.Post, "/api/v1/push/subscriptions", device));
        await notify.CallAsync("Subscribe", "known device (keys updated)", HttpMethod.Post, "/api/v1/push/subscriptions", device);
        await notify.CallAsync("Subscribe", "endpoint is not https", HttpMethod.Post, "/api/v1/push/subscriptions",
            device with { endpoint = "http://push.example.test/device" });
        await notify.CallAsync("SendTestPush", "typical", HttpMethod.Post, "/api/v1/push/test");
        await notify.CallAsync("ListReminders", "upcoming", HttpMethod.Get, "/api/v1/reminders?status=scheduled&limit=20");
        await notify.CallAsync("Unsubscribe", "typical", HttpMethod.Delete, $"/api/v1/push/subscriptions/{subscription}");
        await notify.CallAsync("Unsubscribe", "not found or another user's", HttpMethod.Delete, $"/api/v1/push/subscriptions/{subscription}");
    }

    private static async Task WriteEventSchemasAsync(string directory, CancellationToken cancellationToken)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { TypeInfoResolver = new System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver() };
        ServiceDefaults.Extensions.ConfigureJson(options);
        var events = new (Type Type, string RoutingKey)[]
        {
            (typeof(BatchPlanned), Messaging.RoutingKeys.BatchPlanned),
            (typeof(BatchCompleted), Messaging.RoutingKeys.BatchCompleted),
            (typeof(StarterFed), Messaging.RoutingKeys.StarterFed),
            (typeof(AccountDeleted), Messaging.RoutingKeys.AccountDeleted),
            (typeof(ReminderDue), Messaging.RoutingKeys.ReminderDue),
        };
        foreach (var (type, routingKey) in events)
        {
            var schema = JsonSchemaExporter.GetJsonSchemaAsNode(options, type);
            schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
            schema["title"] = type.Name;
            schema["description"] = $"Body of the '{routingKey}' message on the 'oneloaf.events' topic exchange.";
            await DocsPaths.WriteJsonAsync(Path.Combine(directory, $"{routingKey}.schema.json"), schema, cancellationToken);
        }
    }

    private static string HttpFile(string service, string baseUrls, JsonNode spec, IReadOnlyDictionary<string, List<RecordedExchange>> examples)
    {
        var http = new StringBuilder();
        http.AppendLine(CultureInfo.InvariantCulture, $"# {service}: one typical request per operation, generated from the recorded examples.");
        http.AppendLine(CultureInfo.InvariantCulture, $"# Base URLs: {baseUrls.Replace("`", string.Empty, StringComparison.Ordinal)}");
        http.AppendLine("# Use with the VS Code REST Client or JetBrains HTTP Client; set @token to an access token from POST /api/v1/auth/login.");
        http.AppendLine(CultureInfo.InvariantCulture, $"@baseUrl = http://localhost:{(service == "core-api" ? 5080 : service == "assistant" ? 5081 : 5082)}");
        http.AppendLine("@token = <access token>");
        http.AppendLine();
        foreach (var (path, item) in spec["paths"]!.AsObject())
        {
            foreach (var (method, operation) in item!.AsObject())
            {
                var id = operation?["operationId"]?.GetValue<string>();
                if (id is null || !examples.TryGetValue(id, out var list))
                {
                    continue;
                }

                var example = list.FirstOrDefault(e => e.Status < 400) ?? list[0];
                http.AppendLine(CultureInfo.InvariantCulture, $"### {operation!["summary"]?.GetValue<string>() ?? id}");
                http.AppendLine(CultureInfo.InvariantCulture, $"{method.ToUpperInvariant()} {{{{baseUrl}}}}{example.Path}");
                if (example.Authenticated)
                {
                    http.AppendLine("Authorization: Bearer {{token}}");
                }

                if (example.RequestBody is not null)
                {
                    http.AppendLine("Content-Type: application/json");
                    http.AppendLine();
                    http.AppendLine(example.RequestBody.ToJsonString(DocsPaths.ExampleJson));
                }

                http.AppendLine();
            }
        }

        return http.ToString().ReplaceLineEndings("\n");
    }

    private static string Id(JsonNode? node, string property = "id") =>
        node?[property]?.GetValue<string>() ?? throw new InvalidOperationException($"No {property} in {node?.ToJsonString()}");

    private static ClaudeResponse ToolUse(string name, object input) =>
        new(StopReasons.ToolUse, [new ClaudeToolUseBlock(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(input))], 1450, 64, 1200);

    private static ClaudeResponse Text(string text) => new(StopReasons.EndTurn, [new ClaudeTextBlock(text)], 1890, 92, 1200);

    private sealed record Workspace(string Token, string RecipeId, string? BakeRecordId, DateTimeOffset Start);
}

/// <summary>Locations and writers of the generated documentation.</summary>
public static class DocsPaths
{
    /// <summary>Indented JSON without escaping of non-ASCII characters.</summary>
    public static readonly JsonSerializerOptions ExampleJson = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Repository root.</summary>
    public static string Root
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "OneLoaf.slnx")))
                {
                    return dir.FullName;
                }
            }

            throw new InvalidOperationException("Repository root not found.");
        }
    }

    /// <summary>Writes indented JSON with LF line endings.</summary>
    public static Task WriteJsonAsync(string path, JsonNode node, CancellationToken cancellationToken) =>
        WriteTextAsync(path, node.ToJsonString(ExampleJson) + "\n", cancellationToken);

    /// <summary>Writes text with LF line endings, creating the directory.</summary>
    public static async Task WriteTextAsync(string path, string text, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, text.ReplaceLineEndings("\n"), cancellationToken);
    }
}
