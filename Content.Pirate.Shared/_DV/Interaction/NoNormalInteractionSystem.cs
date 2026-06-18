using Content.Shared.Interaction.Events;

namespace Content.Shared._DV.Interaction;

public sealed class NoNormalInteractionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NoNormalInteractionComponent, InteractionAttemptEvent>(OnInteractionAttempt);
    }

    private void OnInteractionAttempt(Entity<NoNormalInteractionComponent> entity, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }
}
