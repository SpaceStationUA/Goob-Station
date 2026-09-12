// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.MalfAI;
using Content.Shared._Pirate.MalfAI.Actions;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Emp;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio.Systems;

namespace Content.Server._Pirate.MalfAI;

public sealed class MalfAiEmpSystem : EntitySystem
{
    [Dependency] private readonly SharedStationAiSystem _ai = default!;
    [Dependency] private readonly SharedTransformSystem _xforms = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MalfAiMarkerComponent, MalfAiEmpActionEvent>(OnEmp);
    }

    private void OnEmp(Entity<MalfAiMarkerComponent> ent, ref MalfAiEmpActionEvent args)
    {
        if (args.Handled || !_ai.TryGetCore(ent.Owner, out var core) ||
            core.Comp.RemoteEntity is not { } eye || !HasComp<StationAiOverlayComponent>(ent))
            return;

        var originalCore = CompOrNull<MalfAiShuntedComponent>(ent)?.CoreHolder ?? core.Owner;
        var target = args.Target;
        var isIpc = TryComp<HumanoidAppearanceComponent>(target, out var humanoid) && humanoid.Species == "IPC";
        if ((!HasComp<BorgChassisComponent>(target) && !isIpc) ||
            IsProtected(target, ent.Owner, core.Owner, originalCore, eye) ||
            !_xforms.InRange(Transform(target).Coordinates, Transform(eye).Coordinates, 10f) ||
            !_interaction.InRangeUnobstructed(ent.Owner, target))
        {
            _popup.PopupEntity(Loc.GetString("malfai-emp-invalid"), eye, ent.Owner);
            return;
        }

        var position = _xforms.GetMapCoordinates(target);
        // The ordinary EMP helper has no exclusion filter. Reuse its per-entity effects
        // while excluding the AI and owned borgs, including their contained power cells.
        foreach (var uid in _lookup.GetEntitiesInRange(position, 3f))
        {
            if (!IsProtected(uid, ent.Owner, core.Owner, originalCore, eye))
                _emp.TryEmpEffects(uid, 50000f, TimeSpan.FromSeconds(10), ent.Owner);
        }
        Spawn(SharedEmpSystem.EmpPulseEffectPrototype, position);
        _audio.PlayPredicted(SharedEmpSystem.EmpSound, _xforms.ToCoordinates(position), ent.Owner);
        _adminLog.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(ent)} triggered an EMP at {ToPrettyString(target)}.");
        args.Handled = true;
    }

    private bool IsProtected(EntityUid uid, EntityUid ai, EntityUid holder, EntityUid originalCore, EntityUid eye)
    {
        var current = uid;
        while (current.IsValid() && TryComp<TransformComponent>(current, out var xform))
        {
            if (current == ai || current == holder || current == originalCore || current == eye ||
                TryComp<MalfAiControlledComponent>(current, out var controlled) && controlled.Controller == ai)
                return true;
            current = xform.ParentUid;
        }
        return false;
    }
}
