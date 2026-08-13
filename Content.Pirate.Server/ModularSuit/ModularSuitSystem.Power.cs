using Content.Shared.Item.ItemToggle.Components;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class ModularSuitSystem
{
    [Dependency] private PowerCellSystem _powerCell = default!;
    [Dependency] private SharedBatterySystem _battery = default!;

    public const string CellContainer = "cell_slot";

    private void InitializePower()
    {
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitRefreshPowerEvent>(OnRefreshPower);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitInstalledEvent>(OnModuleInstalledRefresh);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitRemovedEvent>(OnModuleRemovedRefresh);
        SubscribeLocalEvent<ModularSuitComponent, ModularSuitModuleToggledEvent>(OnModuleToggledRefresh);
        SubscribeLocalEvent<ModularSuitComponent, EntRemovedFromContainerMessage>(OnCellRemoved);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        ProcessPendingVerbs();

        var query = EntityQueryEnumerator<ModularSuitComponent>();
        while (query.MoveNext(out var uid, out var suit))
        {
            if (GameTiming.CurTime < suit.NextUpdate)
                continue;

            suit.NextUpdate = GameTiming.CurTime + suit.UpdateInterval;
            UpdatePower((uid, suit));
        }
    }

    public override void SetActive(Entity<ModularSuitComponent> ent, bool active)
    {
        if (ent.Comp.Active == active)
            return;

        if (active && !ent.Comp.Assembled)
        {
            Popup.PopupEntity(Loc.GetString("modsuit-not-assembled"), ent, ent.Comp.Wearer ?? ent);
            _audio.PlayPvs(ent.Comp.BuzzSound, ent.Owner);
            UpdateUiState(ent);
            return;
        }

        if (active && !HasCore(ent))
        {
            Popup.PopupEntity(Loc.GetString("modsuit-no-core"), ent, ent.Comp.Wearer ?? ent);
            _audio.PlayPvs(ent.Comp.BuzzSound, ent.Owner);
            UpdateUiState(ent);
            return;
        }

        if (active)
        {
            _audio.PlayPvs(ent.Comp.ActivateSound, ent.Owner);
            _audio.PlayPvs(ent.Comp.NominalSound, ent.Owner);
        }
        else
        {
            _audio.PlayPvs(ent.Comp.DeactivateSound, ent.Owner);
        }

        if (HasComp<ItemToggleComponent>(ent))
        {
            if (active)
                Toggle.TryActivate(ent.Owner, ent.Comp.Wearer, false);
            else
                Toggle.TryDeactivate(ent.Owner, ent.Comp.Wearer, false);
        }

        if (!active && TryComp<ModularSuitEquippedComponent>(ent, out var equipped))
        {
            foreach (var (_, partUid) in equipped.EquippedParts)
            {
                if (TryComp<ItemToggleComponent>(partUid, out var partToggle) && Toggle.IsActivated((partUid, partToggle)))
                    Toggle.TryDeactivate(partUid, ent.Comp.Wearer);
            }

            CheckSuitAssembly(ent.Owner);
        }

        if (active)
        {
            if (ent.Comp.SlotActiveComponents != null && ent.Comp.Wearer != null)
            {
                foreach (var (slot, components) in ent.Comp.SlotActiveComponents)
                {
                    if (Inventory.TryGetSlotEntity(ent.Comp.Wearer.Value, slot, out var targetEntity))
                        EntityManager.AddComponents(targetEntity.Value, components);
                }
            }
        }
        else
        {
            if (ent.Comp.SlotActiveComponents != null && ent.Comp.Wearer != null)
            {
                foreach (var (slot, components) in ent.Comp.SlotActiveComponents)
                {
                    if (Inventory.TryGetSlotEntity(ent.Comp.Wearer.Value, slot, out var targetEntity))
                        EntityManager.RemoveComponents(targetEntity.Value, components);
                }
            }
        }

        base.SetActive(ent, active);

        var ev = new ModularSuitRefreshPowerEvent();
        RaiseLocalEvent(ent, ref ev);

        UpdateUiState(ent);
    }

    private void OnRefreshPower(Entity<ModularSuitComponent> ent, ref ModularSuitRefreshPowerEvent args)
    {
        RefreshPowerState(ent);

        var modules = GetCurrentModules(ent);
        if (!ent.Comp.Active)
        {
            foreach (var module in modules)
            {
                if (TryComp<ModularSuitModuleComponent>(module, out var mod) && mod.IsActive && mod.CanBeDisabled)
                {
                    mod.WasActive = true;
                    mod.IsActive = false;
                    Dirty(module, mod);

                    var ev = new ModularSuitModuleToggledEvent(ent, ent.Comp.Wearer, false);
                    RaiseLocalEvent(module, ref ev);
                }
            }
        }
        else
        {
            foreach (var module in modules)
            {
                if (!TryComp<ModularSuitModuleComponent>(module, out var mod) || !mod.WasActive)
                    continue;

                if (mod.IsActive || !mod.CanBeDisabled)
                    continue;

                if (!CanActivateModule(ent, module))
                    continue;

                mod.IsActive = true;
                mod.WasActive = false;
                Dirty(module, mod);
            }

            foreach (var module in modules)
            {
                if (TryComp<ModularSuitModuleComponent>(module, out var mod) && (mod.IsActive || !mod.CanBeDisabled))
                {
                    var ev = new ModularSuitModuleToggledEvent(ent, ent.Comp.Wearer, true);
                    RaiseLocalEvent(module, ref ev);
                }
            }
        }
    }

    private bool CanActivateModule(Entity<ModularSuitComponent> suit, EntityUid module)
    {
        var attempt = new ModularSuitModuleAttemptEvent(suit.Owner);
        RaiseLocalEvent(module, ref attempt);
        return !attempt.Cancelled;
    }

    private void OnModuleInstalledRefresh(Entity<ModularSuitComponent> ent, ref ModularSuitInstalledEvent args)
    {
        RefreshPowerState(ent);
        UpdateUiState(ent);
    }

    private void OnModuleRemovedRefresh(Entity<ModularSuitComponent> ent, ref ModularSuitRemovedEvent args)
    {
        RefreshPowerState(ent);
        UpdateUiState(ent);
    }

    private void OnModuleToggledRefresh(Entity<ModularSuitComponent> ent, ref ModularSuitModuleToggledEvent args)
    {
        RefreshPowerState(ent);
        UpdateUiState(ent);
    }

    private void OnCellRemoved(Entity<ModularSuitComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != CellContainer)
            return;

        RefreshPowerState(ent);
        UpdateUiState(ent);
    }

    private bool HasCore(Entity<ModularSuitComponent> ent)
    {
        var coreContainer = Container.GetContainer(ent, CoreContainer);
        return coreContainer.ContainedEntities.Count > 0;
    }

    private void RefreshPowerState(Entity<ModularSuitComponent> ent)
    {
        ent.Comp.NextUpdate = GameTiming.CurTime + ent.Comp.UpdateInterval;
    }

    private void UpdatePower(Entity<ModularSuitComponent> suit)
    {
        var coreContainer = Container.GetContainer(suit.Owner, CoreContainer);
        if (coreContainer.ContainedEntities.Count == 0)
        {
            SetActive(suit, false);
            return;
        }

        var coreEnt = coreContainer.ContainedEntities[0];
        if (!TryComp<ModularSuitCoreComponent>(coreEnt, out var core))
        {
            SetActive(suit, false);
            return;
        }

        if (core.Infinite)
            return;

        if (!suit.Comp.Active)
        {
            if (core.Charge < core.MaxCharge && TryChargeFromBattery((coreEnt, core), suit))
                UpdateUiState(suit);

            return;
        }

        float totalDraw = suit.Comp.BasePowerDraw;
        foreach (var module in GetCurrentModules(suit))
        {
            if (TryComp<ModularSuitModuleComponent>(module, out var mod) && mod.IsActive)
                totalDraw += mod.PowerUsage;
        }

        totalDraw *= core.DrawMultiplier;
        if (totalDraw <= 0)
            return;

        var chargeToUse = totalDraw * (float)suit.Comp.UpdateInterval.TotalSeconds;
        var newCharge = Math.Max(0, core.Charge - chargeToUse);
        var used = core.Charge - newCharge;
        core.Charge = newCharge;
        Dirty(coreEnt, core);

        if (used > 0)
        {
            var ev = new ModularSuitChargeChangedEvent(core.Charge, core.MaxCharge);
            RaiseLocalEvent(suit.Owner, ref ev);
        }

        if (core.Charge < core.MaxCharge && TryChargeFromBattery((coreEnt, core), suit))
            UpdateUiState(suit);

        if (core.Charge <= 0)
        {
            SetActive(suit, false);
            UpdateUiState(suit);

            _audio.PlayPvs(suit.Comp.CriticalDestroySound, suit.Owner);
        }

        if (!core.Infinite)
        {
            if (core.Charge <= core.MaxCharge * 0.4f && GameTiming.CurTime >= suit.Comp.NextLowPowerSound)
            {
                suit.Comp.NextLowPowerSound = GameTiming.CurTime + suit.Comp.LowPowerCooldown;
                _audio.PlayPvs(suit.Comp.LowPowerSound, suit.Owner);
            }
        }
    }

    private bool TryChargeFromBattery(Entity<ModularSuitCoreComponent> core, Entity<ModularSuitComponent> suit)
    {
        if (!_powerCell.TryGetBatteryFromSlot(suit.Owner, out var battery))
            return false;

        if (core.Comp.DrawMultiplier <= 0)
            return false;

        var needed = core.Comp.MaxCharge - core.Comp.Charge;
        var maxTransfer = core.Comp.ChargeRate * (float)suit.Comp.UpdateInterval.TotalSeconds;
        var transfer = Math.Min(needed, maxTransfer);
        var batteryCharge = Math.Min(transfer * core.Comp.DrawMultiplier, _battery.GetCharge(battery.Value.AsNullable()));
        transfer = batteryCharge / core.Comp.DrawMultiplier;

        if (batteryCharge <= 0)
            return false;

        if (_powerCell.TryUseCharge(suit.Owner, batteryCharge, predicted: false))
        {
            core.Comp.Charge += transfer;
            Dirty(core.Owner, core.Comp);

            var ev = new ModularSuitChargeChangedEvent(core.Comp.Charge, core.Comp.MaxCharge);
            RaiseLocalEvent(suit.Owner, ref ev);
            return true;
        }

        return false;
    }

    public bool TryUseCoreCharge(Entity<ModularSuitComponent?> suit, float amount)
    {
        if (!Resolve(suit, ref suit.Comp) || !float.IsFinite(amount))
            return false;

        if (amount <= 0)
            return true;

        var coreContainer = Container.GetContainer(suit, CoreContainer);
        if (coreContainer.ContainedEntities.Count == 0)
            return false;

        var coreEnt = coreContainer.ContainedEntities[0];
        if (!TryComp<ModularSuitCoreComponent>(coreEnt, out var core))
            return false;

        if (core.Infinite)
            return true;

        if (core.Charge < amount)
            return false;

        core.Charge -= amount;
        Dirty(coreEnt, core);

        var ev = new ModularSuitChargeChangedEvent(core.Charge, core.MaxCharge);
        RaiseLocalEvent(suit.Owner, ref ev);

        if (core.Charge <= 0 && suit.Comp.Active)
        {
            SetActive((suit.Owner, suit.Comp), false);
            UpdateUiState((suit.Owner, suit.Comp));
        }

        return true;
    }
}
