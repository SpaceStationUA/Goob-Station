// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.MesonGoggles;
using Content.Shared._Pirate.Xray;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.SubFloor;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Clothing.EngineeringGoggles;

/// <summary>Pirate: engineering goggles - synchronizes their vision modes and visuals.</summary>
public sealed class EngineeringGogglesSystem : EntitySystem
{
    private static readonly ResPath RsiPath = new("Clothing/Eyes/Glasses/engineering.rsi");

    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedTrayScannerSystem _trayScanner = default!;
    [Dependency] private readonly SharedXRayVisionSystem _xray = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EngineeringGogglesComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EngineeringGogglesComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<EngineeringGogglesComponent, ToggleEngineeringGogglesEvent>(OnToggleAction);
        SubscribeLocalEvent<EngineeringGogglesComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<EngineeringGogglesComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerb);
    }

    private void OnStartup(Entity<EngineeringGogglesComponent> ent, ref ComponentStartup args)
    {
        UpdateAppearance(ent);
    }

    private void OnGetActions(Entity<EngineeringGogglesComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is null)
            return;

        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        UpdateActionIcon(ent);
        Dirty(ent);
    }

    private void OnToggleAction(Entity<EngineeringGogglesComponent> ent, ref ToggleEngineeringGogglesEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetMode(ent, NextMode(ent.Comp.Mode), args.Performer);
    }

    private void OnActivateInWorld(Entity<EngineeringGogglesComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;

        args.Handled = true;
        SetMode(ent, NextMode(ent.Comp.Mode), args.User);
    }

    private void OnGetAltVerb(Entity<EngineeringGogglesComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var target = ent;
        var user = args.User;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("engineering-goggles-cycle-verb"),
            IconEntity = GetNetEntity(ent.Owner),
            Act = () => SetMode(target, NextMode(target.Comp.Mode), user),
        });
    }

    private static EngineeringGogglesMode NextMode(EngineeringGogglesMode mode)
    {
        return mode switch
        {
            EngineeringGogglesMode.Off => EngineeringGogglesMode.XRay,
            EngineeringGogglesMode.XRay => EngineeringGogglesMode.Tray,
            _ => EngineeringGogglesMode.Off,
        };
    }

    public void SetMode(Entity<EngineeringGogglesComponent> ent, EngineeringGogglesMode mode, EntityUid? user = null)
    {
        var (uid, comp) = ent;
        if (comp.Mode == mode)
            return;

        comp.Mode = mode;
        Dirty(uid, comp);

        // Set the color before toggling so the overlay refresh sees the selected mode.
        if (TryComp<GoggleShaderComponent>(uid, out var shader))
        {
            shader.Color = mode == EngineeringGogglesMode.XRay ? comp.XRayColor : comp.TrayColor;
            Dirty(uid, shader);
        }

        // Disable both modes before enabling the selected one.
        _trayScanner.SetEnabled(uid, false);
        _xray.SetEnabled(uid, false);

        switch (mode)
        {
            case EngineeringGogglesMode.XRay:
                _xray.SetEnabled(uid, true);
                break;
            case EngineeringGogglesMode.Tray:
                _trayScanner.SetEnabled(uid, true);
                break;
        }

        // Pirate: engineering goggles - refresh the shader after the final mode state is set.
        if (shader != null)
        {
            var ev = new GoggleShaderToggledEvent(shader.Enabled);
            RaiseLocalEvent(uid, ref ev);
        }

        var sound = mode == EngineeringGogglesMode.Off ? comp.SoundDeactivate : comp.SoundActivate;
        _audio.PlayPredicted(sound, uid, user);

        UpdateAppearance(ent);
        UpdateActionIcon(ent);
    }

    private void UpdateAppearance(Entity<EngineeringGogglesComponent> ent)
    {
        var (uid, comp) = ent;
        var prefix = comp.Mode switch
        {
            EngineeringGogglesMode.XRay => "xray",
            EngineeringGogglesMode.Tray => "tray",
            _ => null,
        };
        _clothing.SetEquippedPrefix(uid, prefix);
        _appearance.SetData(uid, EngineeringGogglesVisuals.Mode, comp.Mode);
    }

    private void UpdateActionIcon(Entity<EngineeringGogglesComponent> ent)
    {
        var (uid, comp) = ent;
        if (comp.ToggleActionEntity is not { } action)
            return;

        var state = comp.Mode switch
        {
            EngineeringGogglesMode.XRay => "icon-xray",
            EngineeringGogglesMode.Tray => "icon-tray",
            _ => "icon",
        };
        _actions.SetIcon(action, new SpriteSpecifier.Rsi(RsiPath, state));
    }
}
