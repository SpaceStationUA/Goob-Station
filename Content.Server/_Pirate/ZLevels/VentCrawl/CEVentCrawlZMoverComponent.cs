using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.ZLevels.VentCrawl;

/// <summary>
/// Added to a vent-crawling mob while its holder sits in a multi-z pipe adapter, granting it
/// the z-level up/down actions used to traverse between floors through the adapter column.
/// Managed entirely by <see cref="CEVentCrawlZMoverSystem"/>.
/// </summary>
[RegisterComponent]
[Access(typeof(CEVentCrawlZMoverSystem))]
public sealed partial class CEVentCrawlZMoverComponent : Component
{
    [DataField]
    public EntProtoId UpActionProto = "CEActionZLevelUp";

    [DataField]
    public EntProtoId DownActionProto = "CEActionZLevelDown";

    [ViewVariables]
    public EntityUid? ZLevelUpActionEntity;

    [ViewVariables]
    public EntityUid? ZLevelDownActionEntity;

    [ViewVariables]
    public TimeSpan NextMove;
}
