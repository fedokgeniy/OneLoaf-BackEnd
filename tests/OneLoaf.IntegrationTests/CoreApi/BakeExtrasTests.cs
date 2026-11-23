using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.IntegrationTests.Infrastructure;

namespace OneLoaf.IntegrationTests.CoreApi;

/// <summary>Saving a diverged batch as a variant (US-25) and crumb photos (US-51).</summary>
[Collection(ApiTestGroup.Name)]
public sealed class BakeExtrasTests(ApiFixture fixture)
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 1, 2, 3, 4];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task US25_FinishedStageDiffers_VariantTakesTheMeasuredDuration()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var diverged = await CreateBatchAsync(anna, "Diverged bake");
        var stage = diverged.Items[0].Stages.OrderBy(s => s.SortOrder).First();
        await anna.PostAsJsonAsync($"/api/v1/batches/{diverged.Id}/stages/{stage.Id}/start", new StageActionRequest(), CoreApiFactory.Json, Ct);
        await anna.PostAsJsonAsync($"/api/v1/batches/{diverged.Id}/stages/{stage.Id}/complete", new StageActionRequest(), CoreApiFactory.Json, Ct);
        var followed = await CreateBatchAsync(anna, "By the book");

        using var saved = await anna.PostAsJsonAsync($"/api/v1/batches/{diverged.Id}/save-as-variant",
            new SaveAsVariantRequest(Title: "White loaf, quick mix"), CoreApiFactory.Json, Ct);
        var result = await saved.ReadAsync<SaveAsVariantResponse>();
        using var nothing = await anna.PostAsJsonAsync($"/api/v1/batches/{followed.Id}/save-as-variant", new SaveAsVariantRequest(), CoreApiFactory.Json, Ct);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var foreign = await boris.PostAsJsonAsync($"/api/v1/batches/{diverged.Id}/save-as-variant", new SaveAsVariantRequest(), CoreApiFactory.Json, Ct);

        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        Assert.Equal(Seed.R1, result.Recipe.ParentRecipeId);
        var change = Assert.Single(result.Changes);
        Assert.Equal(StageChangeSources.Actual, change.Source);
        Assert.Equal(change.VariantMaxMinutes, result.Recipe.Stages.Single(s => s.Name == change.Name).MaxDurationMinutes);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, nothing.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
    }

    [Fact]
    public async Task US51_UploadListDownloadDelete_OnlyImagesAndOnlyTheOwner()
    {
        using var anna = fixture.CreateClientAs(Seed.Anna);
        var records = await (await anna.GetAsync("/api/v1/bake-records?pageSize=1", Ct)).ReadAsync<OneLoaf.CoreApi.Application.Common.PagedResult<BakeRecordSummaryDto>>();
        var record = records.Items[0].Id;

        using var uploaded = await anna.PostAsync($"/api/v1/bake-records/{record}/photos", Form(Png, "crumb.png", "image/png"), Ct);
        var photo = await uploaded.ReadAsync<BakeRecordPhotoDto>();
        using var notImage = await anna.PostAsync($"/api/v1/bake-records/{record}/photos", Form("hello"u8.ToArray(), "notes.png", "image/png"), Ct);
        var detail = await (await anna.GetAsync($"/api/v1/bake-records/{record}", Ct)).ReadAsync<BakeRecordDto>();
        var bytes = await anna.GetByteArrayAsync(photo.Url, Ct);
        using var boris = fixture.CreateClientAs(Seed.Boris);
        using var foreign = await boris.GetAsync(photo.Url, Ct);
        using var deleted = await anna.DeleteAsync(photo.Url, Ct);
        using var gone = await anna.GetAsync(photo.Url, Ct);

        Assert.Equal(HttpStatusCode.Created, uploaded.StatusCode);
        Assert.Equal("image/png", photo.ContentType);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notImage.StatusCode);
        Assert.Contains(detail.Photos, p => p.Id == photo.Id);
        Assert.Equal(Png, bytes);
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    private static async Task<BatchDto> CreateBatchAsync(HttpClient client, string name)
    {
        var request = new BatchRequest(name, DateTimeOffset.UtcNow.AddDays(10), [new BatchItemRequest(Seed.R1, 2)]);
        return await (await client.PostAsJsonAsync("/api/v1/batches", request, CoreApiFactory.Json, Ct)).ReadAsync<BatchDto>();
    }

    private static MultipartFormDataContent Form(byte[] content, string fileName, string contentType)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { file, PhotoLimits.FormField, fileName } };
    }
}
