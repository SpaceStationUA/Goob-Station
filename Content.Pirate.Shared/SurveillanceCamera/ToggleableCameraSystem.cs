// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Emp;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.SurveillanceCamera;

namespace Content.Pirate.Shared.SurveillanceCamera;

public sealed class ToggleableCameraSystem : EntitySystem
{
    [Dependency] private ItemToggleSystem _toggle = default!;
    [Dependency] private SharedSurveillanceCameraSystem _camera = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ToggleableCameraComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ToggleableCameraComponent, ItemToggleActivateAttemptEvent>(OnActivateAttempt);
        SubscribeLocalEvent<ToggleableCameraComponent, ItemToggledEvent>(OnToggled);
        SubscribeLocalEvent<ToggleableCameraComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnMapInit(Entity<ToggleableCameraComponent> ent, ref MapInitEvent args)
    {
        // ItemToggle raises ItemToggledEvent at map init only when activated.
        if (!_toggle.IsActivated(ent.Owner))
            _camera.SetActive(ent, false);
    }

    private void OnActivateAttempt(Entity<ToggleableCameraComponent> ent, ref ItemToggleActivateAttemptEvent args)
    {
        if (!HasComp<EmpDisabledComponent>(ent))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString(ent.Comp.EmpPopup, ("camera", ent.Owner));
    }

    private void OnToggled(Entity<ToggleableCameraComponent> ent, ref ItemToggledEvent args)
    {
        _camera.SetActive(ent, args.Activated);
    }

    private void OnEmpPulse(Entity<ToggleableCameraComponent> ent, ref EmpPulseEvent args)
    {
        if (!_toggle.IsActivated(ent.Owner))
            return;

        // Require a manual toggle after EMP recovery.
        args.Affected = true;
        args.Disabled = true;
        _toggle.TryDeactivate(ent.Owner, predicted: false);
    }
}
