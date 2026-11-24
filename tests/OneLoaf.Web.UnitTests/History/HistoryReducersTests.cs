using System.Collections.Immutable;
using OneLoaf.Web.Features.History.Services;
using OneLoaf.Web.Features.History.State;
using OneLoaf.Web.Shared.Contracts.Assistant;
using OneLoaf.Web.Shared.Contracts.Core;
using OneLoaf.Web.Shared.State;

namespace OneLoaf.Web.UnitTests.History;

public sealed class HistoryReducersTests
{
    private static readonly HistoryQuery Rated = new(null, 4, null, null);

    [Fact]
    public void OnMore_CurrentQuery_AppendsThePage()
    {
        var state = Loaded(Rated, Bake(), hasMore: true);
        state = HistoryReducers.OnLoadMore(state);

        state = HistoryReducers.OnMore(state, new MoreHistoryLoaded(Rated, new HistoryList([Bake()], 2, 2, false)));

        Assert.False(state.LoadingMore);
        Assert.Equal(2, state.List.Value!.Items.Count);
        Assert.False(state.List.Value.HasMore);
    }

    [Fact]
    public void OnLoaded_StaleQuery_IsIgnored()
    {
        var state = HistoryReducers.OnLoad(Loaded(HistoryQuery.None, Bake(), hasMore: false), new LoadHistory(Rated));

        state = HistoryReducers.OnLoaded(state, new HistoryLoaded(HistoryQuery.None, new Loadable<HistoryList>.Loaded(new HistoryList([], 1, 0, false))));

        Assert.IsType<Loadable<HistoryList>.Loading>(state.List);
        Assert.Equal(Rated, state.Query);
    }

    [Fact]
    public void BakeRecord_AnalysedAndFailed_UpdatesTheRecordAndClearsFlags()
    {
        var id = Guid.NewGuid();
        var record = new BakeRecordDto(id, null, new DateOnly(2026, 11, 21), 1, null, "EUR", 4, "Tight crumb", null, null, [],
            DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, []);
        var state = BakeRecordReducers.OnOpen(Initial(), new OpenBakeRecord(id));
        state = BakeRecordReducers.OnLoaded(state, new BakeRecordLoaded(id, new Loadable<BakeRecordDto>.Loaded(record),
            new Loadable<IReadOnlyList<StageLogEntryDto>>.Loaded([])));
        state = BakeRecordReducers.OnAnalyse(state);

        state = BakeRecordReducers.OnAnalysed(state, new BakeAnalysed(id, new AnalysisDto(id, "Warmer water.", ["26 °C"])));

        Assert.False(state.Analysing);
        Assert.Equal("Warmer water.", state.Record.Value!.AssistantAnalysis);
        Assert.Equal(["26 °C"], state.UnverifiedNumbers);

        state = BakeRecordReducers.OnFailed(BakeRecordReducers.OnUpload(state));
        Assert.False(state.Uploading);
        state = BakeRecordReducers.OnPhoto(state, new PhotoLoaded(id, Guid.NewGuid(), "data:image/png;base64,AA=="));
        Assert.Single(state.PhotoUrls);
    }

    private static BakeRecordState Initial() =>
        new(null, Loadable<BakeRecordDto>.Idle, Loadable<IReadOnlyList<StageLogEntryDto>>.Idle, ImmutableDictionary<Guid, string>.Empty, false, false, false, []);

    private static HistoryState Loaded(HistoryQuery query, BakeRecordSummaryDto bake, bool hasMore) =>
        new(query, new Loadable<HistoryList>.Loaded(new HistoryList([bake], 1, 2, hasMore)), Loadable<IReadOnlyList<BakeStatisticsDto>>.Idle, false);

    private static BakeRecordSummaryDto Bake() =>
        new(Guid.NewGuid(), null, new DateOnly(2026, 11, 21), 1, 2m, "EUR", 4, false, ["Rye"], 0);
}
