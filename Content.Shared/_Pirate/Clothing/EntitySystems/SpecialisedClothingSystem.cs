using Content.Shared._Pirate.Clothing.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.Whitelist;

namespace Content.Shared._Pirate.Clothing.EntitySystems;

public sealed class SpecialisedClothingSystem : EntitySystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private readonly LocId _defaultFailureReason = "specialized-clothing-default-failure";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpecialisedClothingComponent, BeingEquippedAttemptEvent>(OnBeingEquipped);
    }

    private void OnBeingEquipped(Entity<SpecialisedClothingComponent> ent, ref BeingEquippedAttemptEvent args)
    {
        if (_whitelist.IsWhitelistPass(ent.Comp.Whitelist, args.EquipTarget))
            return;

        args.Reason = ent.Comp.FailureReason ?? _defaultFailureReason;
        args.Cancel();
    }
}
