using Content.Shared._Pirate.Bed.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Bed;

[Serializable, NetSerializable]
public enum BedsheetVisuals : byte
{
    Covered,
}

public sealed class BedsheetCoverSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem Appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BedsheetCoverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BedsheetCoverComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<BedsheetCoverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnStartup(Entity<BedsheetCoverComponent> ent, ref ComponentStartup args)
    {
        Appearance.SetData(ent, BedsheetVisuals.Covered, ent.Comp.Covered);
    }

    private void OnGetVerbs(Entity<BedsheetCoverComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new InteractionVerb
        {
            Text = GetCoverVerbText(ent.Comp),
            Act = () => Toggle(ent),
        });
    }

    private void OnGetAlternativeVerbs(Entity<BedsheetCoverComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        args.Verbs.Add(new AlternativeVerb
        {
            Text = GetCoverVerbText(ent.Comp),
            Act = () => Toggle(ent),
        });
    }

    private string GetCoverVerbText(BedsheetCoverComponent component)
    {
        return Loc.GetString(component.Covered
            ? "bedsheet-verb-uncover"
            : "bedsheet-verb-cover");
    }

    private void Toggle(Entity<BedsheetCoverComponent> ent)
    {
        ent.Comp.Covered = !ent.Comp.Covered;
        Dirty(ent);
        Appearance.SetData(ent, BedsheetVisuals.Covered, ent.Comp.Covered);
    }
}
