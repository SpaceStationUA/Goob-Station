// SPDX-License-Identifier: MIT

using Content.Server.Robotics.Systems;
using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.MalfAI;

public sealed class MalfAiSubvertBorgSystem : EntitySystem
{
    [Dependency] private readonly SharedStationAiSystem _ai = default!;
    [Dependency] private readonly SharedStationSystem _stations = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly CyborgLawReceiverSystem _laws = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly NpcFactionSystem _factions = default!;

    private static readonly ProtoId<NpcFactionPrototype> StationFaction = "NanoTrasen";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiSubvertBorgActionEvent>(OnSubvert);
    }

    private void OnSubvert(Entity<MalfAiMarkerComponent> ent, ref MalfAiSubvertBorgActionEvent args)
    {
        if (args.Handled || !_ai.TryGetCore(ent.Owner, out var core) ||
            core.Comp?.RemoteEntity is not { } eye || !HasComp<StationAiOverlayComponent>(ent))
            return;

        var target = args.Target;
        var station = _stations.GetOwningStation(core.Owner);
        if (!HasComp<BorgChassisComponent>(target) || !HasComp<BorgTransponderComponent>(target) ||
            !_factions.IsMember(target, StationFaction) ||
            HasComp<MalfAiControlledComponent>(target) ||
            TryComp<EmaggedComponent>(target, out var emag) && (emag.EmagType & EmagType.Interaction) != 0 ||
            station == null || _stations.GetOwningStation(target) != station ||
            !_xforms.InRange(Transform(target).Coordinates, Transform(eye).Coordinates, 10f) ||
            !_interaction.InRangeUnobstructed(ent.Owner, target))
        {
            _popup.PopupEntity(Loc.GetString("malfai-subvert-borg-invalid"), eye, ent.Owner);
            return;
        }

        _laws.ImposeLawZero(target, ent.Owner);
        args.Handled = TryComp<MalfAiControlledComponent>(target, out var control) && control.Controller == ent.Owner;
        if (!args.Handled)
            return;
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent)} subverted {ToPrettyString(target)}.");
        _popup.PopupEntity(Loc.GetString("malfai-subvert-borg-success"), eye, ent.Owner);
    }
}
