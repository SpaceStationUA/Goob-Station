using Content.Pirate.Shared.LightPaint;
using Content.Server.Forensics;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.LightPaint;

/// <summary>
///     Cleans sprayed paint off a bulb, restoring the colour it had before it was first painted.
///     Silent, unlike most soap interactions -- there's no splash to hear when soap just wipes a
///     dry lacquer off a bulb.
/// </summary>
public sealed class LightPaintRemoverSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPoweredLightSystem _poweredLight = default!;
    [Dependency] private readonly LightPaintSystem _lightPaint = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Soap shares AfterInteract with forensics cleaning, which marks the interaction handled
        // whenever the target has any prints or DNA on it -- which light fixtures pick up as soon
        // as anyone installs a bulb. Removing paint is the more specific intent, so it goes first.
        SubscribeLocalEvent<PaintRemoverComponent, AfterInteractEvent>(OnAfterInteract,
            before: [typeof(ForensicsSystem)]);
        SubscribeLocalEvent<PaintRemoverComponent, LightPaintRemoveDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<PaintRemoverComponent, GetVerbsEvent<UtilityVerb>>(OnVerb);
    }

    private void OnAfterInteract(Entity<PaintRemoverComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = TryStartCleaning(ent, target, args.User);
    }

    private void OnVerb(Entity<PaintRemoverComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !TryGetPaintedBulb(args.Target, out _))
            return;

        var target = args.Target;
        var user = args.User;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("light-paint-remove-verb"),
            // Same icon as forensics' "Remove evidence" -- both are soap wiping something off an object.
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/bubbles.svg.192dpi.png")),
            Act = () => TryStartCleaning(ent, target, user),
        });
    }

    /// <summary>
    ///     Resolves a click to a painted bulb, either directly or through a light fixture.
    /// </summary>
    private bool TryGetPaintedBulb(EntityUid target, out EntityUid bulb)
    {
        bulb = default;

        if (HasComp<PaintedLightBulbComponent>(target))
        {
            bulb = target;
            return true;
        }

        if (!TryComp<PoweredLightComponent>(target, out var light)
            || _poweredLight.GetBulb(target, light) is not { } installed
            || !HasComp<PaintedLightBulbComponent>(installed))
            return false;

        bulb = installed;
        return true;
    }

    private bool TryStartCleaning(Entity<PaintRemoverComponent> ent, EntityUid target, EntityUid user)
    {
        if (!TryGetPaintedBulb(target, out _))
            return false;

        return _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            user,
            ent.Comp.CleanDelay,
            new LightPaintRemoveDoAfterEvent(),
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

    private void OnDoAfter(Entity<PaintRemoverComponent> ent, ref LightPaintRemoveDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        if (!TryGetPaintedBulb(target, out var bulb)
            || !TryComp<PaintedLightBulbComponent>(bulb, out var painted))
            return;

        _lightPaint.PaintBulb(bulb, painted.OriginalColor, remember: false);
        RemComp<PaintedLightBulbComponent>(bulb);

        _popup.PopupEntity(Loc.GetString("light-paint-removed", ("target", bulb)), args.User, args.User);

        args.Handled = true;
    }
}
