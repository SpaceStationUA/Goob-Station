using Content.Pirate.Shared.Alchemy.Components;
using Content.Server.Polymorph.Systems;
using Content.Shared.Item;
using Content.Shared.Polymorph.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Pirate.Server.Alchemy.Systems;

public sealed class AlchemistMimicJuiceSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ChameleonProjectorSystem _chameleon = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AlchemistMimicJuiceComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<AlchemistMimicJuiceComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<AlchemistMimicJuiceComponent> ent, ref ComponentInit args)
    {
        ApplyDisguise(ent);
    }

    private void OnShutdown(Entity<AlchemistMimicJuiceComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.ProjectorEntity is not { } projector)
            return;

        if (!Exists(projector) || Terminating(projector))
            return;

        if (!Deleted(ent.Owner)
            && !Terminating(ent.Owner)
            && TryComp<ChameleonProjectorComponent>(projector, out var projectorComp))
        {
            _chameleon.RevealProjector((projector, projectorComp));
        }

        QueueDel(projector);
    }

    private void ApplyDisguise(Entity<AlchemistMimicJuiceComponent> ent)
    {
        if (TryComp<ChameleonDisguisedComponent>(ent.Owner, out _))
            _chameleon.TryReveal(ent.Owner);

        var (sourceEntity, sourceProto, temporarySource) = PickSource(ent);
        var projector = Spawn(ent.Comp.ProjectorPrototype, Transform(ent.Owner).Coordinates);
        ent.Comp.ProjectorEntity = projector;

        if (TryComp<ChameleonProjectorComponent>(projector, out var projectorComp) && sourceEntity is { } source)
            _chameleon.Disguise((projector, projectorComp), ent.Owner, source);

        if (TryComp<ChameleonDisguisedComponent>(ent.Owner, out var disguised))
            ent.Comp.DisguiseEntity = disguised.Disguise;

        if (ent.Comp.DisguiseEntity is { } disguise)
        {
            if (sourceEntity is { } disguiseSource)
            {
                var meta = MetaData(disguiseSource);
                _meta.SetEntityName(disguise, meta.EntityName);
                _meta.SetEntityDescription(disguise, meta.EntityDescription);
                _appearance.CopyData(disguiseSource, disguise);
            }
            else if (sourceProto is { } protoId && _prototype.TryIndex<EntityPrototype>(protoId, out var proto))
            {
                _meta.SetEntityName(disguise, proto.Name);
                _meta.SetEntityDescription(disguise, proto.Description);
            }
        }

        if (temporarySource is { } temp && Exists(temp))
            QueueDel(temp);
    }

    private (EntityUid? SourceEntity, EntProtoId? SourceProto, EntityUid? TemporarySource) PickSource(Entity<AlchemistMimicJuiceComponent> ent)
    {
        var candidates = new List<EntityUid>();
        foreach (var nearby in _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.SearchRadius, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (nearby == ent.Owner || !HasComp<ItemComponent>(nearby))
                continue;

            candidates.Add(nearby);
        }

        if (candidates.Count > 0)
        {
            var source = _random.Pick(candidates);
            return (source, Prototype(source)?.ID, null);
        }

        if (ent.Comp.FallbackPrototypes.Count == 0)
            return (null, null, null);

        var proto = _random.Pick(ent.Comp.FallbackPrototypes);
        var temp = Spawn(proto, Transform(ent.Owner).Coordinates);
        return (temp, proto, temp);
    }
}
