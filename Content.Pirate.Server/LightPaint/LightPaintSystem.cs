using Content.Pirate.Shared.LightPaint;
using Content.Server.Charges;
using Content.Shared.Charges.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;

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

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightPaintComponent, AfterInteractEvent>(OnAfterInteract);
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
    }

    private void OnAfterInteract(Entity<LightPaintComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryStartPainting(ent, target, args.User);
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
