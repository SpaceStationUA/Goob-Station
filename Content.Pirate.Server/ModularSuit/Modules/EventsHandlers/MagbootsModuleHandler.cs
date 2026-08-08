using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Actions;
using Content.Shared.Inventory;
using Content.Shared.Movement.Systems;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class MagbootsModuleHandler : ModuleActionHandler
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private InventorySystem _inventory = default!;
    [Dependency] private MovementSpeedModifierSystem _speed = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitActionHolderComponent, ToggleMagbootsModuleEvent>(OnToggle);

        SubscribeLocalEvent<ModularSuitMagbootsModuleComponent, ModularSuitModuleToggledEvent>(OnModuleToggled);
        SubscribeLocalEvent<ModularSuitMagbootsModuleComponent, ModularSuitRemovedEvent>(OnModuleRemoved);
    }

    private void OnToggle(Entity<ModularSuitActionHolderComponent> ent, ref ToggleMagbootsModuleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindModuleByAction(ent, args.Action, out var moduleEnt))
            return;

        if (!TryComp<ModularSuitModuleComponent>(moduleEnt, out var moduleComp) || !moduleComp.IsActive)
            return;

        if (!TryComp<ModularSuitMagbootsModuleComponent>(moduleEnt, out var magboots))
            return;

        if (!TryComp<ModularSuitComponent>(ent.Owner, out var suit) || suit.Wearer == null)
            return;

        var enable = !magboots.Enabled;
        if (!SetEnabled((moduleEnt.Value, magboots), ent.Owner, suit.Wearer.Value, enable))
            return;

        Audio.PlayPvs(enable ? args.TurnOnSound : args.TurnOffSound, ent.Owner);
        Popup.PopupEntity(
            Loc.GetString(enable ? "modsuit-magboots-on" : "modsuit-magboots-off"),
            suit.Wearer.Value,
            suit.Wearer.Value);

        _actions.SetToggled(args.Action.Owner, enable);
        args.Handled = true;
    }

    private void OnModuleToggled(Entity<ModularSuitMagbootsModuleComponent> module, ref ModularSuitModuleToggledEvent args)
    {
        if (args.Wearer != null)
            SetEnabled(module, args.Suit, args.Wearer.Value, false);
        else
            module.Comp.Enabled = false;
    }

    private void OnModuleRemoved(Entity<ModularSuitMagbootsModuleComponent> module, ref ModularSuitRemovedEvent args)
    {
        if (TryComp<ModularSuitComponent>(args.Suit, out var suit) && suit.Wearer != null)
            SetEnabled(module, args.Suit, suit.Wearer.Value, false);
        else
            module.Comp.Enabled = false;
    }

    private bool SetEnabled(Entity<ModularSuitMagbootsModuleComponent> module, EntityUid suit, EntityUid wearer, bool enabled)
    {
        if (module.Comp.ActiveComponents == null)
            return false;

        if (module.Comp.Enabled == enabled)
            return false;

        if (!_inventory.TryGetSlotEntity(wearer, module.Comp.TargetSlot, out var boots)
            || !HasComp<ModularSuitPartComponent>(boots))
        {
            return false;
        }

        if (enabled)
            EntityManager.AddComponents(boots.Value, module.Comp.ActiveComponents);
        else
            EntityManager.RemoveComponents(boots.Value, module.Comp.ActiveComponents);

        module.Comp.Enabled = enabled;
        _speed.RefreshMovementSpeedModifiers(wearer);
        return true;
    }
}
