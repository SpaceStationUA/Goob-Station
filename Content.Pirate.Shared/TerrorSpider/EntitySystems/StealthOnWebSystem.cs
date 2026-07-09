using Content.Shared.Damage.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Spider;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Pirate.Shared.TerrorSpider.EntitySystems;

public sealed class StealthOnWebSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StealthOnWebComponent, StartCollideEvent>(OnEntityEnter);
        SubscribeLocalEvent<StealthOnWebComponent, EndCollideEvent>(OnEntityExit);
        SubscribeLocalEvent<StealthOnWebComponent, UnbuckledEvent>(OnUnbuckled);
        SubscribeLocalEvent<StealthOnWebComponent, StaminaMeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<StealthOnWebComponent> ent, ref StaminaMeleeHitEvent args)
    {
        if (!TryComp<StealthComponent>(ent.Owner, out var stealth))
        {
            args.Multiplier *= 0.1f;
            return;
        }

        var t = (_stealth.GetVisibility(ent.Owner) - stealth.MinVisibility) / (stealth.MaxVisibility - stealth.MinVisibility);
        args.Multiplier *= Math.Clamp(1 - t, 0.1f, 1f);
    }

    private void OnEntityExit(Entity<StealthOnWebComponent> ent, ref EndCollideEvent args)
    {
        if (_timing.InPrediction || !HasComp<SpiderWebObjectComponent>(args.OtherEntity))
            return;

        if (ent.Comp.Contacts.Remove(GetContact(args)))
            UpdateStealth(ent);
    }

    private void OnEntityEnter(Entity<StealthOnWebComponent> ent, ref StartCollideEvent args)
    {
        if (_timing.InPrediction || !HasComp<SpiderWebObjectComponent>(args.OtherEntity))
            return;

        ent.Comp.Contacts.Add(GetContact(args));
        UpdateStealth(ent);
    }

    private void OnUnbuckled(Entity<StealthOnWebComponent> ent, ref UnbuckledEvent args)
    {
        if (_timing.InPrediction || !HasComp<SpiderWebObjectComponent>(args.Strap))
            return;

        // Pirate: a terror web cocoon is both a strap and a web object. If the physics
        // contact is lost during buckling, clear that web contact when the spider exits.
        var strap = args.Strap.Owner;
        if (ent.Comp.Contacts.RemoveWhere(contact => contact.Other == strap) > 0)
            UpdateStealth(ent);
    }

    private void UpdateStealth(Entity<StealthOnWebComponent> ent)
    {
        if (ent.Comp.Contacts.Count != 0)
        {
            EnsureComp<StealthComponent>(ent.Owner);
            EnsureComp<StealthOnMoveComponent>(ent.Owner);
            return;
        }

        RemComp<StealthComponent>(ent.Owner);
        RemComp<StealthOnMoveComponent>(ent.Owner);
    }

    private static SpiderWebContact GetContact(StartCollideEvent args)
        => new(args.OtherEntity, args.OurFixtureId, args.OtherFixtureId);

    private static SpiderWebContact GetContact(EndCollideEvent args)
        => new(args.OtherEntity, args.OurFixtureId, args.OtherFixtureId);
}
