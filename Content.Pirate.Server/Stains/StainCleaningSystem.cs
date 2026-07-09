using Content.Pirate.Shared.Stains.Components;
using Content.Server.Forensics;
using Content.Shared.DoAfter;
using Content.Shared.Forensics;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Popups;

namespace Content.Pirate.Server.Stains;

public sealed class StainCleaningSystem : EntitySystem
{
    [Dependency] private readonly SharedDoAfterSystem _doAfter = null!;
    [Dependency] private readonly SharedPopupSystem _popup = null!;
    [Dependency] private readonly StainSystem _stains = null!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StainCleanerComponent, AfterInteractEvent>(OnAfterInteract, after: [typeof(ForensicsSystem)]);
        SubscribeLocalEvent<StainCleanerComponent, CleanStainsDoAfterEvent>(OnCleanDoAfter);
        SubscribeLocalEvent<StainableComponent, CleanForensicsDoAfterEvent>(OnCleanForensicsStainDoAfter, after: [typeof(ForensicsSystem)]);
        SubscribeLocalEvent<InventoryComponent, CleanForensicsDoAfterEvent>(OnCleanForensicsInventoryDoAfter, after: [typeof(ForensicsSystem)]);
    }

    private void OnAfterInteract(Entity<StainCleanerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.CleanDelay,
            new CleanStainsDoAfterEvent(),
            ent.Owner,
            target: args.Target,
            used: ent.Owner)
        {
            NeedHand = true,
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.01f,
        });
    }

    private void OnCleanDoAfter(Entity<StainCleanerComponent> ent, ref CleanStainsDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Args.Target == null)
            return;

        args.Handled = true;

        if (_stains.CleanEntityAndEquipment(args.Args.Target.Value))
            _popup.PopupEntity(Loc.GetString("stain-cleaned"), args.Args.Target.Value, args.Args.User);
    }

    private void OnCleanForensicsStainDoAfter(Entity<StainableComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        _stains.TryCleanStain(ent.Owner);
    }

    private void OnCleanForensicsInventoryDoAfter(Entity<InventoryComponent> ent, ref CleanForensicsDoAfterEvent args)
    {
        if (args.Cancelled)
            return;

        _stains.CleanEntityAndEquipment(ent.Owner);
    }
}
