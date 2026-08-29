using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;

namespace Content.Pirate.Shared.Tools;

/// <summary>
/// Lets a tool refine part of a stack into something else without destroying the rest of it.
/// </summary>
public sealed class StackRefinableSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StackRefinableComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<StackRefinableComponent, StackRefineDoAfterEvent>(OnDoAfter);
    }

    private void OnInteractUsing(Entity<StackRefinableComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only complain about the count if they're actually holding the right tool, otherwise every
        // interaction with a short stack would nag.
        if (!_tool.HasQuality(args.Used, ent.Comp.QualityNeeded))
            return;

        if (_stack.GetCount(ent.Owner) < ent.Comp.Cost)
        {
            _popup.PopupClient(Loc.GetString("stack-refinable-not-enough",
                ("count", ent.Comp.Cost), ("item", ent.Owner)), ent, args.User);
            return;
        }

        args.Handled = _tool.UseTool(
            args.Used,
            args.User,
            ent,
            ent.Comp.RefineTime,
            ent.Comp.QualityNeeded,
            new StackRefineDoAfterEvent(),
            fuel: ent.Comp.RefineFuel);
    }

    private void OnDoAfter(Entity<StackRefinableComponent> ent, ref StackRefineDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_net.IsClient)
            return;

        // The stack can shrink while the do-after runs, so re-check before consuming.
        if (!_stack.TryUse(ent.Owner, ent.Comp.Cost))
            return;

        args.Handled = true;

        for (var i = 0; i < ent.Comp.ResultAmount; i++)
        {
            SpawnNextToOrDrop(ent.Comp.RefineResult, ent);
        }

        // Keep going while there's enough left, so a big stack doesn't need a click per pair.
        // TryUse deletes the stack when it empties, so re-check existence before repeating.
        if (Exists(ent) && !TerminatingOrDeleted(ent) && _stack.GetCount(ent.Owner) >= ent.Comp.Cost)
            args.Repeat = true;
    }
}
