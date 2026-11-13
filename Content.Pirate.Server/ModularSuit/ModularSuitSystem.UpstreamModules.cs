using Content.Goobstation.Shared.ModSuits;
using Content.Goobstation.Shared.ModSuits.Components;
using Content.Goobstation.Shared.ModSuits.Systems;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.ActionBlocker;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitSystem
{
    [Dependency] private ModSuitSystem _upstreamModules = default!;
    [Dependency] private ActionBlockerSystem _moduleActionBlocker = default!;

    private void InitializeUpstreamModules()
    {
        SubscribeLocalEvent<UpstreamSuitModuleComponent, MapInitEvent>(OnUpstreamModuleInit);
        SubscribeLocalEvent<UpstreamSuitModuleComponent, ModularSuitInstalledEvent>(OnUpstreamModuleInstalled);
        SubscribeLocalEvent<UpstreamSuitModuleComponent, ModularSuitRemovedEvent>(OnUpstreamModuleRemoved);
        SubscribeLocalEvent<UpstreamSuitModuleComponent, ModularSuitModuleToggledEvent>(OnUpstreamModuleToggled);
        SubscribeLocalEvent<UpstreamSuitModuleComponent, ModularSuitModuleAttemptEvent>(OnUpstreamModuleAttempt);
        SubscribeLocalEvent<UpstreamSuitModuleComponent, ComponentShutdown>(OnUpstreamModuleShutdown);
        SubscribeLocalEvent<ModularSuitComponent, ExternalModModuleCommandEvent>(OnExternalModuleCommand);
        SubscribeLocalEvent<ModularSuitComponent, ConsumeUpstreamModuleChargeEvent>(OnConsumeModuleCharge);
    }

    private void OnUpstreamModuleInit(Entity<UpstreamSuitModuleComponent> ent, ref MapInitEvent args)
    {
        if (!TryComp<ModModuleComponent>(ent, out var upstream)
            || !TryComp<ModularSuitModuleComponent>(ent, out var local))
            return;

        local.PowerUsage = upstream.IdlePower + upstream.ActivePower;
        local.PowerInstanceUsage = upstream.UsePower;
        local.IsPermanent |= !upstream.Removable;
        local.WasActive = upstream.Type == ModModuleType.Passive;
        Dirty(ent, local);
    }

    private bool CanInstallUpstreamModule(Entity<ModularSuitComponent> suit, Entity<ModularSuitModuleComponent> module, EntityUid user)
    {
        if (!HasComp<UpstreamSuitModuleComponent>(module)
            || !TryComp<ModModuleComponent>(module, out var upstream))
            return true;

        foreach (var other in GetCurrentModules(suit))
        {
            if (!_whitelist.IsWhitelistPass(upstream.Incompatible, other)
                && !(TryComp<ModModuleComponent>(other, out var otherModule)
                    && _whitelist.IsWhitelistPass(otherModule.Incompatible, module)))
                continue;

            ModuleFailure(user, "modsuit-module-incompatible");
            return false;
        }

        // Pirate parts can be installed after modules; required parts are checked on activation.
        return true;
    }

    private void OnUpstreamModuleInstalled(Entity<UpstreamSuitModuleComponent> ent, ref ModularSuitInstalledEvent args)
    {
        if (!TryComp<ModModuleComponent>(ent, out var upstream)
            || !TryComp<ModularSuitComponent>(args.Suit, out var suit))
            return;

        _upstreamModules.BindExternalModule((ent.Owner, upstream), args.Suit);
        ent.Comp.LastWearer = suit.Wearer;
        var installed = new ModModuleInstalledEvent(args.Suit);
        RaiseLocalEvent(ent, ref installed);

        if (suit.Wearer is { } wearer)
            _upstreamModules.GrantExternalModuleAction((ent.Owner, upstream), wearer);

        if (upstream.Type == ModModuleType.Passive
            && ((upstream.AllowFlags & ModModuleAllowFlags.Inactive) != 0
                || suit.Active && suit.Wearer is { } user && CanActivateUpstreamModule((args.Suit, suit), (ent.Owner, upstream), user)))
            ApplyUpstreamModuleState((args.Suit, suit), (ent.Owner, upstream), true);
    }

    private void OnUpstreamModuleRemoved(Entity<UpstreamSuitModuleComponent> ent, ref ModularSuitRemovedEvent args)
    {
        DetachUpstreamModule(ent);
    }

    private void OnUpstreamModuleShutdown(Entity<UpstreamSuitModuleComponent> ent, ref ComponentShutdown args)
    {
        DetachUpstreamModule(ent);
    }

    private void DetachUpstreamModule(Entity<UpstreamSuitModuleComponent> ent)
    {
        if (!TryComp<ModModuleComponent>(ent, out var upstream) || upstream.Suit is not { } suit)
            return;

        if (upstream.Active)
        {
            _upstreamModules.SyncExternalModuleActive((ent.Owner, upstream), false);
            var deactivated = new ModModuleDeactivatedEvent(suit, ent.Comp.LastWearer);
            RaiseLocalEvent(ent, ref deactivated);
        }

        if (TryComp<ModularSuitModuleComponent>(ent, out var local))
        {
            local.IsActive = false;
            local.WasActive = upstream.Type == ModModuleType.Passive;
            Dirty(ent, local);
        }

        if (ent.Comp.LastWearer is { } wearer)
            _upstreamModules.RemoveExternalModuleAction((ent.Owner, upstream), wearer);

        var removed = new ModModuleRemovedEvent(suit);
        RaiseLocalEvent(ent, ref removed);
        _upstreamModules.BindExternalModule((ent.Owner, upstream), null);
        ent.Comp.LastWearer = null;
    }

    protected override void OnUpstreamModuleWearerChanged(Entity<ModularSuitComponent> suit, EntityUid? oldWearer, EntityUid? newWearer)
    {
        if (oldWearer == newWearer)
            return;

        foreach (var module in GetCurrentModules(suit))
        {
            if (!TryComp<UpstreamSuitModuleComponent>(module, out var bridge)
                || !TryComp<ModModuleComponent>(module, out var upstream))
                continue;

            if (oldWearer is { } old)
                _upstreamModules.RemoveExternalModuleAction((module, upstream), old);

            bridge.LastWearer = newWearer;
            var changed = new ModSuitWearerChangedEvent(oldWearer, newWearer);
            RaiseLocalEvent(module, ref changed);

            if (newWearer is { } wearer)
                _upstreamModules.GrantExternalModuleAction((module, upstream), wearer);
        }
    }

    private void OnUpstreamModuleToggled(Entity<UpstreamSuitModuleComponent> ent, ref ModularSuitModuleToggledEvent args)
    {
        if (!TryComp<ModModuleComponent>(ent, out var upstream) || upstream.Active == args.Activated)
            return;

        // Active tools must be deliberately redeployed after a suit shutdown.
        if (!args.Activated && upstream.Type != ModModuleType.Passive
            && TryComp<ModularSuitModuleComponent>(ent, out var local))
            local.WasActive = false;

        _upstreamModules.SyncExternalModuleActive((ent.Owner, upstream), args.Activated);
        ent.Comp.LastWearer = args.Wearer;
        if (args.Activated)
        {
            var activated = new ModModuleActivatedEvent(args.Suit, args.Wearer);
            RaiseLocalEvent(ent, ref activated);
        }
        else
        {
            var deactivated = new ModModuleDeactivatedEvent(args.Suit, args.Wearer);
            RaiseLocalEvent(ent, ref deactivated);
        }
    }

    private void OnUpstreamModuleAttempt(Entity<UpstreamSuitModuleComponent> ent, ref ModularSuitModuleAttemptEvent args)
    {
        if (!TryComp<ModularSuitComponent>(args.Suit, out var suit)
            || suit.Wearer is not { } user
            || !TryComp<ModModuleComponent>(ent, out var upstream)
            || !CanActivateUpstreamModule((args.Suit, suit), (ent.Owner, upstream), user))
            args.Cancel();
    }

    private bool ModuleReady(Entity<ModularSuitComponent> suit, Entity<ModModuleComponent> module, EntityUid user)
    {
        if (module.Comp.Suit != suit.Owner
            || !Container.TryGetContainer(suit, ModuleContainer, out var modules) || !modules.Contains(module))
            return false;

        if ((module.Comp.AllowFlags & ModModuleAllowFlags.Unworn) == 0 && suit.Comp.Wearer != user)
            return ModuleFailure(user, "modsuit-module-not-worn");

        if ((module.Comp.AllowFlags & ModModuleAllowFlags.Inactive) == 0 && !suit.Comp.Active)
            return ModuleFailure(user, "modsuit-module-suit-inactive");

        if ((module.Comp.AllowFlags & ModModuleAllowFlags.Incapacitated) == 0 && !_moduleActionBlocker.CanInteract(user, null))
            return false;

        if (GameTiming.CurTime < module.Comp.NextUse)
            return ModuleFailure(user, "modsuit-module-cooldown");

        if (!_upstreamModules.TryGetSuitModuleHost(suit, out var host))
            return false;

        foreach (var slot in module.Comp.RequiredParts)
        {
            if (!host.PartEntities.ContainsKey(slot))
                return ModuleFailure(user, "modsuit-module-missing-part");
        }

        if ((module.Comp.UsePower > 0 || module.Comp.ActivePower > 0 || module.Comp.IdlePower > 0)
            && !_upstreamModules.HasCharge(host, Math.Max(module.Comp.UsePower, 0.001f)))
            return ModuleFailure(user, "modsuit-no-power");

        return true;
    }

    private bool CanActivateUpstreamModule(Entity<ModularSuitComponent> suit, Entity<ModModuleComponent> module, EntityUid user)
    {
        if (!ModuleReady(suit, module, user))
            return false;

        var attempt = new ModModuleActivateAttemptEvent(suit, user);
        RaiseLocalEvent(module, ref attempt);
        if (!attempt.Cancelled)
            return true;

        if (attempt.Reason != null)
            ModuleFailure(user, attempt.Reason);
        return false;
    }

    private bool ModuleFailure(EntityUid user, string key)
    {
        Popup.PopupEntity(Loc.GetString(key), user, user);
        return false;
    }

    private void RequestUpstreamModuleToggle(Entity<ModularSuitComponent> suit, EntityUid module, EntityUid user, bool active)
    {
        if (!TryComp<ModModuleComponent>(module, out var upstream)
            || suit.Comp.Wearer != user && (upstream.AllowFlags & ModModuleAllowFlags.Unworn) == 0)
            return;

        if (upstream.Type == ModModuleType.Passive)
        {
            if (!active || CanActivateUpstreamModule(suit, (module, upstream), user))
                ApplyUpstreamModuleState(suit, (module, upstream), active);
            return;
        }

        if (upstream.Type == ModModuleType.Usable ? active : upstream.Active != active)
            _upstreamModules.TryUseModule(module, user);
    }

    private void OnExternalModuleCommand(Entity<ModularSuitComponent> suit, ref ExternalModModuleCommandEvent args)
    {
        if (!HasComp<UpstreamSuitModuleComponent>(args.Module)
            || !TryComp<ModModuleComponent>(args.Module, out var upstream)
            || upstream.Suit != suit.Owner
            || !Container.TryGetContainer(suit, ModuleContainer, out var modules)
            || !modules.Contains(args.Module))
            return;

        Entity<ModModuleComponent> module = (args.Module, upstream);
        if (args.Command == ExternalModModuleCommand.SetActive)
        {
            if (!args.Active || suit.Comp.Wearer is { } wearer && CanActivateUpstreamModule(suit, module, wearer))
            {
                ApplyUpstreamModuleState(suit, module, args.Active);
                args.Succeeded = true;
            }
            return;
        }

        if (args.User is not { } user
            || suit.Comp.Wearer != user && (upstream.AllowFlags & ModModuleAllowFlags.Unworn) == 0)
            return;

        if (args.Command == ExternalModModuleCommand.Use && upstream.Type is (ModModuleType.Toggle or ModModuleType.Active) && upstream.Active)
        {
            ApplyUpstreamModuleState(suit, module, false);
            args.Succeeded = true;
            return;
        }

        if (!ModuleReady(suit, module, user))
            return;

        if (args.Command == ExternalModModuleCommand.StartUse)
        {
            if (!upstream.Active || !TryUseCoreCharge(suit.AsNullable(), upstream.UsePower, deferDeactivation: true))
                return;
        }
        else if (upstream.Type == ModModuleType.Usable)
        {
            var used = new ModModuleUsedEvent(suit, user);
            RaiseLocalEvent(module, ref used);
            if (!used.Handled || !TryUseCoreCharge(suit.AsNullable(), upstream.UsePower, deferDeactivation: true))
                return;
        }
        else
        {
            if (upstream.Type == ModModuleType.Passive || !CanActivateUpstreamModule(suit, module, user)
                || !TryUseCoreCharge(suit.AsNullable(), upstream.UsePower, deferDeactivation: true))
                return;

            if (upstream.Type == ModModuleType.Active)
            {
                foreach (var other in GetCurrentModules(suit))
                {
                    if (other != module.Owner && TryComp<ModModuleComponent>(other, out var otherModule)
                        && otherModule.Type == ModModuleType.Active && otherModule.Active)
                        ApplyUpstreamModuleState(suit, (other, otherModule), false);
                }
            }

            ApplyUpstreamModuleState(suit, module, true);
        }

        _upstreamModules.StartExternalModuleCooldown(module);
        args.Succeeded = true;
        UpdateUiState(suit);
    }

    private void ApplyUpstreamModuleState(Entity<ModularSuitComponent> suit, Entity<ModModuleComponent> module, bool active)
    {
        if (!TryComp<ModularSuitModuleComponent>(module, out var local) || local.IsActive == active && module.Comp.Active == active)
            return;

        local.IsActive = active;
        local.WasActive = false;
        Dirty(module, local);
        var toggled = new ModularSuitModuleToggledEvent(suit, suit.Comp.Wearer, active);
        RaiseLocalEvent(module, ref toggled);
        UpdateUiState(suit);
    }

    private void OnConsumeModuleCharge(Entity<ModularSuitComponent> ent, ref ConsumeUpstreamModuleChargeEvent args)
    {
        args.Succeeded = TryUseCoreCharge(ent.AsNullable(), args.Amount, deferDeactivation: true);
    }
}
