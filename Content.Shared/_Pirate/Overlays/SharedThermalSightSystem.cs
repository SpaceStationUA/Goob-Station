// SPDX-License-Identifier: MIT
using Content.Shared._Pirate.Clothing.MesonGoggles;
using Content.Shared.Actions;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Pirate.Overlays;

public abstract class SharedThermalSightSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ThermalSightComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ThermalSightComponent, ToggleThermalSightEvent>(OnToggleAction);
    }

    private void OnGetActions(Entity<ThermalSightComponent> ent, ref GetItemActionsEvent args)
    {
        if (ent.Comp.ToggleAction is not { } action)
            return;

        args.AddAction(ref ent.Comp.ToggleActionEntity, action);
    }

    private void OnToggleAction(Entity<ThermalSightComponent> ent, ref ToggleThermalSightEvent args)
    {
        if (args.Handled)
            return;

        SetEnabled(ent, !ent.Comp.Enabled, args.Performer);
        args.Handled = true;
    }

    public void SetEnabled(Entity<ThermalSightComponent> ent, bool enabled, EntityUid? user = null)
    {
        if (ent.Comp.Enabled == enabled)
            return;

        ent.Comp.Enabled = enabled;
        Dirty(ent);

        if (TryComp(ent, out GoggleShaderComponent? goggleShader))
        {
            goggleShader.Enabled = enabled;
            Dirty(ent.Owner, goggleShader);

            var ev = new GoggleShaderToggledEvent(enabled);
            RaiseLocalEvent(ent.Owner, ref ev);
        }

        _appearance.SetData(ent, ThermalSightVisual.Visual, enabled ? ThermalSightVisual.On : ThermalSightVisual.Off);

        var sound = enabled ? ent.Comp.SoundOn : ent.Comp.SoundOff;
        _audio.PlayPredicted(sound, ent, user);
    }
}
