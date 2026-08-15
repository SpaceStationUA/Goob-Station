using Content.Shared._Pirate.Reputation;
using Content.Shared.Actions;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mind;

namespace Content.Shared._Pirate.Implants;

/// <summary>
/// Assigns an uplink implant's contracts mind to the contracts mind.
/// </summary>
public sealed class UplinkImplantSystem : EntitySystem
{
    [Dependency] private readonly ReputationSystem _reputation = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StoreContractsComponent, ImplantImplantedEvent>(OnImplanted);
        SubscribeLocalEvent<StoreContractsComponent, ImplantRemovedEvent>(OnRemoved);
        SubscribeLocalEvent<StoreContractsComponent, StoreContractsMindChangedEvent>(OnMindChanged);
    }

    private void OnImplanted(Entity<StoreContractsComponent> ent, ref ImplantImplantedEvent args)
    {
        var mob = args.Implanted;

        // don't overwrite if the mind is valid
        if (_reputation.GetContracts(ent.Comp.Mind) != null)
            return;

        // implanting into SSD people won't let them use contracts but whatever
        if (_mind.GetMind(mob) is not {} mind)
            return;

        // giving non-traitors an uplink implant won't let them buy rep-gated gear
        if (_reputation.GetContracts(mind) is not {} contracts)
            return;

        _reputation.SetStoreMind(ent, mind);
    }

    private void OnRemoved(Entity<StoreContractsComponent> ent, ref ImplantRemovedEvent args)
    {
        _actions.RemoveProvidedActions(args.Implanted, ent.Owner);
        _reputation.SetStoreMind(ent, null);
    }

    private void OnMindChanged(Entity<StoreContractsComponent> ent, ref StoreContractsMindChangedEvent args)
    {
        if (args.Mind is not {} mind ||
            _reputation.GetContracts(mind) is null ||
            !TryComp<SubdermalImplantComponent>(ent.Owner, out var implant) ||
            implant.ImplantedEntity is not {} mob)
        {
            return;
        }

        _actions.AddAction(mob, ref ent.Comp.ActionId, ent.Comp.Action, ent.Owner);
    }
}
