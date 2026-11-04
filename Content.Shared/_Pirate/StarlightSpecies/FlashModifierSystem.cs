using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.StarlightSpecies;

/// <summary>Permanent flash sensitivity supplied by Starlight's cybernetic eyes.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class FlashModifierComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Modifier = 1f;
}

public sealed class FlashModifierSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FlashModifierComponent, FlashEffectDurationEvent>(OnFlash);
    }

    private void OnFlash(Entity<FlashModifierComponent> ent, ref FlashEffectDurationEvent args)
    {
        if (float.IsFinite(ent.Comp.Modifier) && ent.Comp.Modifier > 0)
            args.Duration *= ent.Comp.Modifier;
    }
}

/// <summary>Modifies blindness and its associated slowdown, independently of an explicitly supplied stun duration.</summary>
[ByRefEvent]
public record struct FlashEffectDurationEvent(TimeSpan Duration);
