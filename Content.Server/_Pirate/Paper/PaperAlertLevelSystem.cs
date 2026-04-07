using Content.Server.AlertLevel;
using Content.Shared._Pirate.Paper;

namespace Content.Server._Pirate.Paper;

public sealed class PaperAlertLevelSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlertLevelComponent, PaperGetStationAlertLevelEvent>(OnGetStationAlertLevel);
    }

    private void OnGetStationAlertLevel(Entity<AlertLevelComponent> ent, ref PaperGetStationAlertLevelEvent args)
    {
        args.AlertLevel = ent.Comp.CurrentLevel;
    }
}
