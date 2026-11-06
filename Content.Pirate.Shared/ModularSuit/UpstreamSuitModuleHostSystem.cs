using Content.Goobstation.Shared.ModSuits;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Pirate.Shared.ModularSuit;

/// <summary>Exposes our suit state to the shared upstream module effects, including client prediction.</summary>
public sealed partial class UpstreamSuitModuleHostSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitComponent, QueryModSuitModuleHostEvent>(OnQueryHost);
        SubscribeLocalEvent<ModularSuitModuleComponent, QueryModSuitModuleHostEvent>(OnQueryModuleHost);
        SubscribeLocalEvent<ModularSuitComponent, ExternalModSuitChargeEvent>(OnCharge);
    }

    private void OnQueryHost(Entity<ModularSuitComponent> ent, ref QueryModSuitModuleHostEvent args)
    {
        if (!_container.TryGetContainer(ent, SharedModularSuitSystem.ModuleContainer, out var modules))
            return;

        var parts = TryComp<ModularSuitEquippedComponent>(ent, out var equipped)
            ? new Dictionary<string, EntityUid>(equipped.EquippedParts)
            : new Dictionary<string, EntityUid>();
        if (_container.TryGetContainer(ent, SharedModularSuitSystem.PartContainer, out var stowed))
        {
            foreach (var part in stowed.ContainedEntities)
            {
                if (!TryComp<ModularSuitPartComponent>(part, out var comp))
                    continue;

                var slot = comp.PartType switch
                {
                    SuitPartType.Helmet => "head",
                    SuitPartType.Torso => "outerClothing",
                    SuitPartType.Gloves => "gloves",
                    SuitPartType.Boots => "shoes",
                    _ => null,
                };
                if (slot != null)
                    parts.TryAdd(slot, part);
            }
        }
        args.Host = new(ent, ent.Comp.Wearer, parts, modules);
    }

    private void OnQueryModuleHost(Entity<ModularSuitModuleComponent> ent, ref QueryModSuitModuleHostEvent args)
    {
        if (_container.TryGetContainingContainer((ent.Owner, null, null), out var container)
            && container.ID == SharedModularSuitSystem.ModuleContainer
            && HasComp<ModularSuitComponent>(container.Owner))
            RaiseLocalEvent(container.Owner, ref args);
    }

    private void OnCharge(Entity<ModularSuitComponent> ent, ref ExternalModSuitChargeEvent args)
    {
        if (!float.IsFinite(args.Amount) || args.Amount < 0
            || !_container.TryGetContainer(ent, SharedModularSuitSystem.CoreContainer, out var cores)
            || cores.ContainedEntities.Count == 0
            || !TryComp<ModularSuitCoreComponent>(cores.ContainedEntities[0], out var core)
            || !core.Infinite && core.Charge < args.Amount)
            return;

        if (!args.Consume || _net.IsClient)
        {
            args.Succeeded = true;
            return;
        }

        var consume = new ConsumeUpstreamModuleChargeEvent(args.Amount);
        RaiseLocalEvent(ent, ref consume);
        args.Succeeded = consume.Succeeded;
    }
}

[ByRefEvent]
public record struct ConsumeUpstreamModuleChargeEvent(float Amount)
{
    public bool Succeeded;
}
