using OneLoaf.CoreApi.Application.Batches;
using OneLoaf.CoreApi.Application.History;
using OneLoaf.Domain;

namespace OneLoaf.CoreApi.UnitTests.Production;

public sealed class BakeExtrasUnitTests
{
    private static readonly DateTimeOffset Start = new(2026, 12, 5, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void US25_FinishedStage_UsesMeasuredMinutes()
    {
        var proposed = BatchVariantService.Proposed(Stage(StageStatus.Done, actualMinutes: 275));

        Assert.Equal((275, 275, StageChangeSources.Actual), proposed);
    }

    [Fact]
    public void US25_ExtendedPendingStage_ShiftsBothEnds()
    {
        var proposed = BatchVariantService.Proposed(Stage(StageStatus.Pending, extended: 30));

        Assert.Equal((270, 330, StageChangeSources.Planned), proposed);
    }

    [Fact]
    public void US25_UntouchedStage_ProposesNothing() => Assert.Null(BatchVariantService.Proposed(Stage(StageStatus.Pending)));

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }, "image/jpeg")]
    [InlineData(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, "image/png")]
    [InlineData(new byte[] { 0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4, 0x57, 0x45, 0x42, 0x50 }, "image/webp")]
    [InlineData(new byte[] { 0x3C, 0x73, 0x76, 0x67 }, null)]
    public void US51_FormatIsDetectedBySignature(byte[] header, string? expected) =>
        Assert.Equal(expected, PhotoFormats.Detect(header)?.ContentType);

    private static BatchStageDto Stage(StageStatus status, int? actualMinutes = null, int extended = 0) =>
        new(Guid.NewGuid(), 2, "Bulk fermentation", "bulk_fermentation", 240, 300, Start, Start.AddMinutes(240), Start.AddMinutes(300),
            actualMinutes is null ? null : Start, actualMinutes is { } m ? Start.AddMinutes(m) : null, status, extended, false, null, null);
}
