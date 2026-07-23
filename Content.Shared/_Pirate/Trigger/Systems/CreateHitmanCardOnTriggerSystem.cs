// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Trigger.Components.Effects;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Trigger;

namespace Content.Shared._Pirate.Trigger.Systems;

public sealed class CreateHitmanCardOnTriggerSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CreateHitmanCardOnTriggerComponent, TriggerEvent>(OnTrigger);
    }

    private void OnTrigger(Entity<CreateHitmanCardOnTriggerComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != null && !ent.Comp.KeysIn.Contains(args.Key))
            return;

        if (args.Handled || args.User is not { } user)
            return;

        var coordinates = _transform.GetMapCoordinates(ent);
        var card = EntityManager.PredictedSpawn("HitmanBusinessCard", coordinates);
        _hands.TryPickupAnyHand(user, card);
        args.Handled = true;
    }
}
