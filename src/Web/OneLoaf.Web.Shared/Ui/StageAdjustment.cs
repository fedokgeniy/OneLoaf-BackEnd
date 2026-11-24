using OneLoaf.Web.Shared.Contracts.Core;

namespace OneLoaf.Web.Shared.Ui;

/// <summary>The baker asked to change the time of a stage.</summary>
/// <param name="Stage">The stage.</param>
/// <param name="Minutes">Minutes to add; negative makes the stage shorter.</param>
public sealed record StageAdjustment(BatchStageDto Stage, int Minutes);
