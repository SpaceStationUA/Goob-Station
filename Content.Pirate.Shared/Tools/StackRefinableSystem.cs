using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tools.Systems;
using Robust.Shared.Network;

namespace Content.Pirate.Shared.Tools;

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

        // Avoid a shortage popup for unrelated tools.
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

        // The stack can shrink during the do-after.
        if (!_stack.TryUse(ent.Owner, ent.Comp.Cost))
            return;

        args.Handled = true;

        for (var i = 0; i < ent.Comp.ResultAmount; i++)
        {
            SpawnNextToOrDrop(ent.Comp.RefineResult, ent);
        }

        // Repeat while another refinement can be paid for.
        if (Exists(ent) && !TerminatingOrDeleted(ent) && _stack.GetCount(ent.Owner) >= ent.Comp.Cost)
            args.Repeat = true;
    }
}
