using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.Maths;

namespace Content.Pirate.Shared.Witch.Systems;

public sealed class SharedWitchMindFogSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchMindFogComponent, MoveInputEvent>(OnMoveInput);
    }

    private void OnMoveInput(Entity<WitchMindFogComponent> ent, ref MoveInputEvent args)
    {
        var buttons = args.Entity.Comp.HeldMoveButtons;
        var preserved = buttons & ~MoveButtons.AnyDirection;
        var remapped = MoveButtons.None;

        if ((buttons & MoveButtons.Up) != 0)
            remapped |= ToButton(ent.Comp.UpDirection);

        if ((buttons & MoveButtons.Down) != 0)
            remapped |= ToButton(ent.Comp.DownDirection);

        if ((buttons & MoveButtons.Left) != 0)
            remapped |= ToButton(ent.Comp.LeftDirection);

        if ((buttons & MoveButtons.Right) != 0)
            remapped |= ToButton(ent.Comp.RightDirection);

        args.Entity.Comp.HeldMoveButtons = SharedMoverController.GetNormalizedMovement(preserved | remapped);
    }

    private static MoveButtons ToButton(Direction direction)
    {
        return direction switch
        {
            Direction.North => MoveButtons.Up,
            Direction.South => MoveButtons.Down,
            Direction.West => MoveButtons.Left,
            Direction.East => MoveButtons.Right,
            _ => MoveButtons.None,
        };
    }
}
