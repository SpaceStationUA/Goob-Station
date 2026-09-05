using System.Numerics;
using Content.Pirate.Shared.LightPaint;
using Content.Server.Charges;
using Content.Server.Crayon;
using Content.Server.Decals;
using Content.Shared.Charges.Components;
using Content.Shared.Crayon;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Pirate.Server.LightPaint;

public sealed class LightPaintSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedLightBulbSystem _bulb = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly SharedPointLightSystem _pointLight = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ChargesSystem _charges = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedCrayonSystem _crayon = default!;
    [Dependency] private readonly DecalSystem _decals = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Draw glyphs with the can's paint colour.
        SubscribeLocalEvent<LightPaintComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(CrayonSystem)]);
        SubscribeLocalEvent<LightPaintComponent, LightPaintDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<LightPaintComponent, LightPaintColorSelectedMessage>(OnColorSelected);
        SubscribeLocalEvent<LightPaintComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<LightPaintComponent> ent, ref MapInitEvent args)
    {
        UpdateCanVisuals(ent);
    }

    private void OnColorSelected(Entity<LightPaintComponent> ent, ref LightPaintColorSelectedMessage args)
    {
        ent.Comp.Color = args.Color;
        Dirty(ent);
        UpdateCanVisuals(ent);
    }

    private void UpdateCanVisuals(Entity<LightPaintComponent> ent)
    {
        _appearance.SetData(ent, LightPaintVisuals.Color, ent.Comp.Color);

        if (TryComp<CrayonComponent>(ent, out var crayon))
        {
            _crayon.SetColor((ent.Owner, crayon), ent.Comp.Color);
            _ui.SetUiState(ent.Owner, CrayonUiKey.Key,
                new CrayonBoundUserInterfaceState(crayon.SelectedState, crayon.SelectableColor, crayon.Color));
        }
    }

    private void OnAfterInteract(Entity<LightPaintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (args.Target is { } target && TryStartPainting(ent, target, args.User))
        {
            args.Handled = true;
            return;
        }

        args.Handled = TryDrawGlyph(ent, args.ClickLocation, args.User);
    }

    private bool TryGetBulb(EntityUid target, out EntityUid bulb)
    {
        bulb = default;

        if (HasComp<LightBulbComponent>(target))
        {
            bulb = target;
            return true;
        }

        if (!TryComp<PoweredLightComponent>(target, out var light))
            return false;

        if (_poweredLight.GetBulb(target, light) is not { } installed)
            return false;

        bulb = installed;
        return true;
    }

    private bool TryStartPainting(Entity<LightPaintComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryGetBulb(target, out _))
        {
            if (HasComp<PoweredLightComponent>(target))
            {
                _popup.PopupEntity(Loc.GetString("light-paint-no-bulb", ("target", target)), user, user);
                return true;
            }

            return false;
        }

        if (TryComp<LimitedChargesComponent>(ent, out var charges)
            && _charges.GetCurrentCharges((ent, charges)) < ent.Comp.ChargeCost)
        {
            _popup.PopupEntity(Loc.GetString("light-paint-empty", ("used", ent.Owner)), user, user);
            return true;
        }

        return _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            ent.Comp.Delay,
            new LightPaintDoAfterEvent(),
            ent,
            target: target,
            used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTarget,
        });
    }

    private bool TryDrawGlyph(Entity<LightPaintComponent> ent, EntityCoordinates clickLocation, EntityUid user)
    {
        if (!TryComp<CrayonComponent>(ent, out var crayon) || string.IsNullOrEmpty(crayon.SelectedState))
            return false;

        if (!clickLocation.IsValid(EntityManager))
            return false;

        if (TryComp<LimitedChargesComponent>(ent, out var charges)
            && _charges.GetCurrentCharges((ent, charges)) < ent.Comp.ChargeCost)
        {
            _popup.PopupEntity(Loc.GetString("light-paint-empty", ("used", ent.Owner)), user, user);
            return true;
        }

        if (!_decals.TryAddDecal(crayon.SelectedState, clickLocation.Offset(new Vector2(-0.5f, -0.5f)), out _, ent.Comp.Color, cleanable: true))
            return false;

        _audio.PlayPvs(ent.Comp.Spray, ent);
        _charges.TryUseCharges((ent, charges), ent.Comp.ChargeCost);

        // Advance queued text in the crayon UI.
        _ui.ServerSendUiMessage(ent.Owner, CrayonUiKey.Key, new CrayonUsedMessage(crayon.SelectedState));

        return true;
    }

    private void OnDoAfter(Entity<LightPaintComponent> ent, ref LightPaintDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!TryGetBulb(target, out var bulb))
            return;

        if (TryComp<LimitedChargesComponent>(ent, out var charges)
            && !_charges.TryUseCharges((ent, charges), ent.Comp.ChargeCost))
            return;

        PaintBulb(bulb, ent.Comp.Color, remember: true);

        _audio.PlayPvs(ent.Comp.Spray, ent);
        _popup.PopupEntity(Loc.GetString("light-paint-success", ("target", bulb)), args.User, args.User);

        args.Handled = true;
    }

    public void PaintBulb(EntityUid bulb, Color color, bool remember)
    {
        if (!TryComp<LightBulbComponent>(bulb, out var bulbComp))
            return;

        if (remember && !HasComp<PaintedLightBulbComponent>(bulb))
        {
            var painted = EnsureComp<PaintedLightBulbComponent>(bulb);
            painted.OriginalColor = bulbComp.Color;
            Dirty(bulb, painted);
        }

        _bulb.SetColor(bulb, color, bulbComp);
        RefreshFixture(bulb, color);
    }

    private void RefreshFixture(EntityUid bulb, Color color)
    {
        if (Transform(bulb).ParentUid is not { Valid: true } parent
            || !TryComp<PoweredLightComponent>(parent, out var light)
            || _poweredLight.GetBulb(parent, light) != bulb)
            return;

        _pointLight.SetColor(parent, color);

        // Force the stock visualizer to refresh the fixture glow layer.
        _appearance.SetData(parent, PaintedLightFixtureVisuals.BulbColor, color);
    }
}
