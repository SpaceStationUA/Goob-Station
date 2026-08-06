using Content.Shared.Movement.Pulling.Events;
using Content.Shared.Pulling.Events;
using Content.Shared.Storage.Components;

namespace Content.Shared._DV.GlimmerWisp;

/// <summary>
/// Blocks pulling, grabbing, and entity-storage insertion for entities with
/// <see cref="UncapturableComponent"/>.
/// </summary>
public sealed class SharedUncapturableSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UncapturableComponent, PullAttemptEvent>(OnPullAttempt);
        SubscribeLocalEvent<UncapturableComponent, BeingPulledAttemptEvent>(OnBeingPulledAttempt);
        SubscribeLocalEvent<UncapturableComponent, InsertIntoEntityStorageAttemptEvent>(OnInsertAttempt);
    }

    private void OnPullAttempt(Entity<UncapturableComponent> ent, ref PullAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnBeingPulledAttempt(Entity<UncapturableComponent> ent, ref BeingPulledAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnInsertAttempt(Entity<UncapturableComponent> ent, ref InsertIntoEntityStorageAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
