using Content.Shared._Pirate.StarlightSpecies.Magic.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Magic;

namespace Content.Shared._Pirate.StarlightSpecies.Magic.Systems;

/// <summary>Starlight's temporary hand-held cantrips, using native spell prerequisites.</summary>
public sealed class CantripSystem : EntitySystem
{
    [Dependency] private readonly SharedMagicSystem _magic = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    public override void Initialize() => SubscribeLocalEvent<SpawnItemInHandEvent>(OnSpawn);

    private void OnSpawn(SpawnItemInHandEvent args)
    {
        if (args.Handled || !_magic.PassesSpellPrerequisites(args.Action, args.Performer))
            return;
        var item = PredictedSpawnAtPosition(args.Spawned, Transform(args.Performer).Coordinates);
        var after = new AfterSpawnItemInHandEvent { Entity = item, Performer = args.Performer };
        RaiseLocalEvent(args.Action, ref after);
        if (!_hands.TryPickupAnyHand(args.Performer, item) && args.RequiresFreeHand)
        {
            QueueDel(item);
            return;
        }
        if (args.SelfDamage != null)
            _damage.TryChangeDamage(args.Performer, args.SelfDamage, true);
        args.Handled = true;
    }
}
