using Content.Server.Fax;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Fax.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Paper;
using Content.Shared.Station.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Gangwars;

public sealed class GangDirectiveFaxSystem : GameRuleSystem<GangDirectiveFaxComponent>
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly FaxSystem _fax = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override void Started(EntityUid uid, GangDirectiveFaxComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        component.SendAt = Timing.CurTime + component.Delay;
    }

    protected override void ActiveTick(EntityUid uid, GangDirectiveFaxComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.Sent || Timing.CurTime < component.SendAt)
            return;

        component.Sent = true;
        SendDirective(component);
    }

    private void SendDirective(GangDirectiveFaxComponent component)
    {
        var paper = _proto.Index(component.Paper);
        if (!paper.TryGetComponent<PaperComponent>(out var paperComp, EntityManager.ComponentFactory))
        {
            Log.Error($"{component.Paper} has no {nameof(PaperComponent)}, gang directive not sent.");
            return;
        }

        // The paper prototype supplies stamps, so FaxPrintout needs no extra stamps.
        var printout = new FaxPrintout(Loc.GetString(paperComp.Content), paper.Name, prototypeId: paper.ID);

        var grids = new HashSet<EntityUid>();
        foreach (var station in _station.GetStationsSet())
        {
            // Crewed stations only, not CentCom or other station-like maps.
            if (HasComp<StationJobsComponent>(station) &&
                TryComp<StationDataComponent>(station, out var stationData))
                grids.UnionWith(stationData.Grids);
        }

        var query = EntityQueryEnumerator<FaxMachineComponent, TransformComponent>();
        while (query.MoveNext(out var faxUid, out var fax, out var xform))
        {
            if (xform.GridUid is not { } grid || !grids.Contains(grid))
                continue;

            if (component.FaxKeywords.Exists(keyword => fax.FaxName.Contains(keyword, StringComparison.Ordinal)))
                _fax.Receive(faxUid, printout, null, fax);
        }
    }
}
