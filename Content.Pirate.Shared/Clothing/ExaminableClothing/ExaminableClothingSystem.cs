using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Clothing.ExaminableClothing;

public sealed class ExaminableClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExaminableClothingComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnWearerExamined);
    }

    private string ExamineText(Entity<ExaminableClothingComponent> ent, EntityUid wearer)
    {
        if (ent.Comp.ExamineText is { } examineText)
        {
            return Loc.GetString(
                "examinable-clothing-examine",
                ("wearer", wearer),
                ("item", Loc.GetString(examineText, ("wearer", wearer))));
        }

        return Loc.GetString(
            "examinable-clothing-examine",
            ("wearer", wearer),
            ("item", FormattedMessage.EscapeText(Identity.Name(ent, EntityManager))));
    }

    private void OnExamined(Entity<ExaminableClothingComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("examinable-clothing-when-worn", ("message", ExamineText(ent, args.Examiner))));
    }

    private void OnWearerExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        var enumerator = new InventorySystem.InventorySlotEnumerator(ent.Comp, SlotFlags.WITHOUT_POCKET);
        while (enumerator.NextItem(out var item, out var slot))
        {
            if (!TryComp<ExaminableClothingComponent>(item, out var examinable) ||
                (slot.SlotFlags & examinable.AllowedSlots) == SlotFlags.NONE)
                continue;

            args.PushMarkup(ExamineText(new Entity<ExaminableClothingComponent>(item, examinable), ent.Owner));
        }
    }
}
