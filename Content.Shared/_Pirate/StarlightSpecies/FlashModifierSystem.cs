using Content.Shared.Flash;
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
        SubscribeLocalEvent<FlashModifierComponent, FlashDurationMultiplierEvent>(OnFlash);
    }

    private void OnFlash(EntityUid uid, FlashModifierComponent component, FlashDurationMultiplierEvent args)
        => args.Multiplier *= component.Modifier;
}
