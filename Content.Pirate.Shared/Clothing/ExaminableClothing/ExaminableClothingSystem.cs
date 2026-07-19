using System.Linq;
using Content.Pirate.Shared.Clothing.Lanyards;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;
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
            if ((slot.SlotFlags & SlotFlags.NECK) != SlotFlags.NONE && HasComp<LanyardComponent>(item))
                AddLanyardExamine(item, ent.Owner, args);

            if (!TryComp<ExaminableClothingComponent>(item, out var examinable) ||
                (slot.SlotFlags & examinable.AllowedSlots) == SlotFlags.NONE)
                continue;

            args.PushMarkup(ExamineText(new Entity<ExaminableClothingComponent>(item, examinable), ent.Owner));
        }
    }

    private void AddLanyardExamine(EntityUid item, EntityUid wearer, ExaminedEvent args)
    {
        if (!TryComp<PaperLabelComponent>(item, out var label) ||
            label.LabelSlot.Item is not { Valid: true } paperUid ||
            !TryComp<PaperComponent>(paperUid, out var paper))
        {
            return;
        }

        using (args.PushGroup(nameof(LanyardComponent)))
        {
            var user = Identity.Entity(wearer, EntityManager);
            if (!args.IsInDetailsRange)
            {
                args.PushMarkup(Loc.GetString("comp-lanyard-has-lanyard-cant-read", ("user", user)));
                return;
            }

            if (string.IsNullOrWhiteSpace(paper.Content))
            {
                args.PushMarkup(Loc.GetString("comp-lanyard-has-lanyard-blank", ("user", user)));
                return;
            }

            args.PushMarkup(Loc.GetString("comp-lanyard-has-lanyard", ("user", user)));
            args.PushMarkup(paper.Content.TrimEnd());

            if (paper.StampedBy.Count == 0)
                return;

            var stamps = string.Join(", ", paper.StampedBy.Select(s => Loc.GetString(s.StampedName)));
            args.PushMarkup(Loc.GetString("comp-lanyard-examine-detail-stamped-by", ("stamps", stamps)));
        }
    }
}
