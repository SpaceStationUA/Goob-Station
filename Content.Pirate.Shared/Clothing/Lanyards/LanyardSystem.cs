using System.Linq;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Labels.Components;
using Content.Shared.Paper;

namespace Content.Pirate.Shared.Clothing.Lanyards;

public sealed class LanyardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InventoryComponent, ExaminedEvent>(OnWearerExamined);
    }

    private void OnWearerExamined(Entity<InventoryComponent> ent, ref ExaminedEvent args)
    {
        var enumerator = new InventorySystem.InventorySlotEnumerator(ent.Comp, SlotFlags.NECK);
        while (enumerator.NextItem(out var item))
        {
            if (!HasComp<LanyardComponent>(item) ||
                !TryComp<PaperLabelComponent>(item, out var label) ||
                label.LabelSlot.Item is not { Valid: true } paperUid ||
                !TryComp<PaperComponent>(paperUid, out var paper))
            {
                continue;
            }

            AddLanyardExamine(ent.Owner, paper, args);
        }
    }

    private void AddLanyardExamine(EntityUid wearer, PaperComponent paper, ExaminedEvent args)
    {
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
