// Pirate: preserve Starlight's pollen lifetime, merge radius and emission intervals with native botany.
using Content.Server.Botany.Components;
using Content.Shared._Pirate.StarlightSpecies.Pollen.Components;
using Content.Shared.Storage.Components;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.StarlightSpecies.Pollen;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class PollenMarkerComponent : Component
{
    public string ScentId = string.Empty;
    public bool IsPollen => true;
    public EntityUid? ContainedIn;
    [AutoPausedField] public TimeSpan ExpiresAt;
}

public sealed class PollenEmitterSystem : EntitySystem
{
    [Dependency] private readonly PollenCatalogSystem _catalog = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ProduceComponent, MapInitEvent>(OnProduce);
        SubscribeLocalEvent<PlantHolderComponent, MapInitEvent>(OnTray);
    }

    private void OnProduce(Entity<ProduceComponent> ent, ref MapInitEvent args)
    {
        if (MetaData(ent).EntityPrototype is { } proto && _catalog.TryGetProduce(proto.ID, out _))
            EnsureComp<EmitPollenComponent>(ent).PollenId = proto.ID;
    }

    private void OnTray(Entity<PlantHolderComponent> ent, ref MapInitEvent args) => EnsureComp<EmitPollenComponent>(ent);

    public EntityUid? GetAirtightContainer(TransformComponent xform)
    {
        var owner = xform.Owner;
        while (_containers.TryGetContainingContainer(owner, out var container))
        {
            owner = container.Owner;
            if (TryComp<EntityStorageComponent>(owner, out var storage) && storage.Airtight && !storage.Open)
                return owner;
        }
        return null;
    }

    public override void Update(float frameTime)
    {
        var now = _timing.CurTime;
        var expired = EntityQueryEnumerator<PollenMarkerComponent>();
        while (expired.MoveNext(out var marker, out var info))
            if (now >= info.ExpiresAt)
                QueueDel(marker);

        var emitters = EntityQueryEnumerator<EmitPollenComponent, TransformComponent>();
        while (emitters.MoveNext(out var uid, out var emitter, out var xform))
        {
            if (emitter.NextEmitTime == TimeSpan.Zero)
                emitter.NextEmitTime = now + RollDelay(emitter);
            if (now < emitter.NextEmitTime)
                continue;
            emitter.NextEmitTime = now + RollDelay(emitter);

            var pollenId = emitter.PollenId;
            if (pollenId == null && TryComp<PlantHolderComponent>(uid, out var tray) && !tray.Dead && tray.Seed != null)
            {
                foreach (var product in tray.Seed.ProductPrototypes)
                    if (_catalog.TryGetProduce(product, out _))
                    {
                        pollenId = product;
                        break;
                    }
            }
            if (pollenId == null)
                continue;

            var position = _transform.GetMapCoordinates(xform);
            var containedIn = GetAirtightContainer(xform);
            if (TryComp<PollenMarkerComponent>(emitter.LastMarkerEntity, out var previous) &&
                previous.ScentId == pollenId && previous.ContainedIn == containedIn &&
                position.InRange(_transform.GetMapCoordinates(emitter.LastMarkerEntity!.Value), emitter.MergeRadius))
            {
                previous.ExpiresAt = now + TimeSpan.FromSeconds(emitter.PollenLifetime);
                continue;
            }

            var marker = Spawn(null, position);
            var pollen = AddComp<PollenMarkerComponent>(marker);
            pollen.ScentId = pollenId;
            pollen.ContainedIn = containedIn;
            pollen.ExpiresAt = now + TimeSpan.FromSeconds(emitter.PollenLifetime);
            emitter.LastMarkerEntity = marker;
        }
    }

    private TimeSpan RollDelay(EmitPollenComponent emitter)
    {
        var variance = emitter.EmitInterval * emitter.EmitIntervalVariance;
        return TimeSpan.FromSeconds(Math.Max(emitter.MinEmitInterval,
            emitter.EmitInterval + _random.NextFloat(-variance, variance)));
    }
}
