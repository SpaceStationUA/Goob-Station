// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Clothing.MesonGoggles;
using Content.Shared._Pirate.Xray;
using Content.Shared.Actions;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.SubFloor;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Shared._Pirate.Clothing.EngineeringGoggles;

/// <summary>
/// Pirate: engineering goggles - cycles <see cref="EngineeringGogglesComponent"/> through its three modes,
/// driving TrayScanner/XRayVision mutually exclusively and keeping the item's own icon, action icon, on-body
/// sprite and goggle shader color all in lockstep. See EngineeringGogglesComponent for the mode mapping.
/// </summary>
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

        // Set the shader color for the NEW mode before toggling either sub-system's Enabled below. Each of
        // those raises GoggleShaderToggledEvent, and the client refreshes its overlay's cached color by
        // reading GoggleShaderComponent.Color at that exact moment - if we toggled first and recolored after,
        // the client would always render one mode behind (e.g. showing xray's color while t-ray is enabling).
        if (TryComp<GoggleShaderComponent>(uid, out var shader))
        {
            shader.Color = mode == EngineeringGogglesMode.XRay ? comp.XRayColor : comp.TrayColor;
            Dirty(uid, shader);
        }

        // Disable both sub-modes first (each call is a no-op if already off), then enable the target one last -
        // both of these also flip GoggleShaderComponent.Enabled, so whichever runs last decides the end state.
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

        // Pirate: engineering goggles - the two calls above each raise their own GoggleShaderToggledEvent as a
        // side effect of enabling/disabling their own component, using whatever Color happened to be set at
        // that exact instant. Rather than depend on getting that interleaving exactly right, re-raise it one
        // last time now that Color and Enabled are both in their final state for this mode, so the client's
        // cached overlay color can never end up one step behind.
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
