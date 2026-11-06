using Content.Goobstation.Shared.ModSuits.Components;

namespace Content.Goobstation.Shared.ModSuits.Systems;

// Pirate: retain upstream module effects while Pirate owns deployment, UI and core power.
public sealed partial class ModSuitSystem
{
    public bool TryGetSuitModuleHost(EntityUid suit, out ModSuitModuleHost host)
    {
        if (TryComp<ModSuitComponent>(suit, out var native))
        {
            host = new(suit, native.Wearer, native.PartEntities, native.ModulesContainer);
            return true;
        }

        var query = new QueryModSuitModuleHostEvent();
        RaiseLocalEvent(suit, ref query);
        host = query.Host.GetValueOrDefault();
        return query.Host.HasValue;
    }

    public bool TryGetModuleHost(EntityUid module, out ModSuitModuleHost host)
    {
        host = default;
        if (TryComp<ModModuleComponent>(module, out var comp)
            && comp.Suit is { } suit
            && TryGetSuitModuleHost(suit, out host))
            return true;

        // Our existing drill can also drive the upstream sphere without becoming a second device module.
        var query = new QueryModSuitModuleHostEvent();
        RaiseLocalEvent(module, ref query);
        host = query.Host.GetValueOrDefault();
        return query.Host.HasValue;
    }

    public EntityUid? GetPart(ModSuitModuleHost host, string slot)
    {
        return host.PartEntities.TryGetValue(slot, out var part) ? part : null;
    }

    public bool HasCharge(ModSuitModuleHost host, float amount)
    {
        if (TryComp<ModSuitComponent>(host.Owner, out var native))
            return HasCharge((host.Owner, native), amount);

        var query = new ExternalModSuitChargeEvent(amount, false);
        RaiseLocalEvent(host.Owner, ref query);
        return query.Succeeded;
    }

    public bool TryUseCharge(ModSuitModuleHost host, float amount)
    {
        if (TryComp<ModSuitComponent>(host.Owner, out var native))
            return TryUseCharge((host.Owner, native), amount);

        var query = new ExternalModSuitChargeEvent(amount, true);
        RaiseLocalEvent(host.Owner, ref query);
        return query.Succeeded;
    }

    public void SetModuleActive(ModSuitModuleHost host, Entity<ModModuleComponent> module, bool active)
    {
        if (TryComp<ModSuitComponent>(host.Owner, out var native))
        {
            SetModuleActive((host.Owner, native), module, active);
            return;
        }

        var command = new ExternalModModuleCommandEvent(module, host.Wearer, ExternalModModuleCommand.SetActive, active);
        RaiseLocalEvent(host.Owner, ref command);
    }

    private bool SendExternalModuleCommand(EntityUid suit, EntityUid module, EntityUid user, ExternalModModuleCommand operation)
    {
        var command = new ExternalModModuleCommandEvent(module, user, operation);
        RaiseLocalEvent(suit, ref command);
        return command.Succeeded;
    }

    public void BindExternalModule(Entity<ModModuleComponent> module, EntityUid? suit)
    {
        module.Comp.Suit = suit;
        Dirty(module);
    }

    public void SyncExternalModuleActive(Entity<ModModuleComponent> module, bool active)
    {
        if (module.Comp.Active == active)
            return;

        module.Comp.Active = active;
        Dirty(module);
        if (module.Comp.ActionEntity is { } action)
            _actions.SetToggled(action, active);
    }

    public void GrantExternalModuleAction(Entity<ModModuleComponent> module, EntityUid wearer)
    {
        EnsureModuleAction(module);
        GrantModuleAction(module, wearer);
    }

    public void RemoveExternalModuleAction(Entity<ModModuleComponent> module, EntityUid wearer)
    {
        RemoveModuleAction(module, wearer);
    }

    public void StartExternalModuleCooldown(Entity<ModModuleComponent> module)
    {
        StartCooldown(module);
    }
}
