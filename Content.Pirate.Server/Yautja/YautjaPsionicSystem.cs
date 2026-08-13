using Content.Shared._DV.Psionics.Components;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Server.Yautja;

/// <summary>
/// Yautja are not psionically inclined. The base humanoid species grants everyone
/// <see cref="PotentialPsionicComponent"/>, so strip it from Yautja when they spawn.
/// </summary>
public sealed class YautjaPsionicSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        // Note: we deliberately subscribe on HumanoidAppearanceComponent rather than
        // PotentialPsionicComponent, because SharedPsionicSystem already subscribes that
        // (component, event) pair and Robust forbids duplicate directed subscriptions.
        SubscribeLocalEvent<HumanoidAppearanceComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<HumanoidAppearanceComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Species != "Yautja")
            return;

        RemComp<PotentialPsionicComponent>(ent);
    }
}
