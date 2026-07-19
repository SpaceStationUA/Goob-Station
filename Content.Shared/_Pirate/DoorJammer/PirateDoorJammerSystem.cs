using Content.Shared.Doors.Components;
using Content.Shared.Doors.Systems;
using Content.Shared.Projectiles;

namespace Content.Shared._Pirate.DoorJammer;

/// <summary>
///     Controls bolting and unbolting a door while a door jammer is embedded.
/// </summary>
public sealed partial class PirateDoorJammerSystem : EntitySystem
{
    [Dependency] private readonly SharedDoorSystem _door = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateDoorJammerComponent, EmbedEvent>(OnEmbed);
        SubscribeLocalEvent<PirateDoorJammerComponent, EmbedDetachEvent>(OnEmbedDetach);
    }

    private void OnEmbed(Entity<PirateDoorJammerComponent> ent, ref EmbedEvent args)
    {
        if (!HasComp<DoorComponent>(args.Embedded) || !TryComp<DoorBoltComponent>(args.Embedded, out var doorBolt))
            return;

        ent.Comp.WasAlreadyBolted = _door.IsBolted(args.Embedded, doorBolt);
        Dirty(ent);

        if (ent.Comp.WasAlreadyBolted.Value)
            return;

        // Pirate: door jammers force bolts even when a local power check would reject normal bolting.
        _door.TrySetBoltDown((args.Embedded, doorBolt), true, predicted: true, requirePower: false);
    }

    private void OnEmbedDetach(Entity<PirateDoorJammerComponent> ent, ref EmbedDetachEvent args)
    {
        if (!HasComp<DoorComponent>(args.Embedded) || !TryComp<DoorBoltComponent>(args.Embedded, out var doorBolt))
            return;

        if (ent.Comp.WasAlreadyBolted is false)
            _door.TrySetBoltDown((args.Embedded, doorBolt), false, args.Detacher, predicted: true, requirePower: false);

        ent.Comp.WasAlreadyBolted = null;
        Dirty(ent);
    }
}
