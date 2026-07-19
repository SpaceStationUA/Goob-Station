using System;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Blinking;

public enum BlinkingWipeDirection : byte
{
    TopDown = 0,
    BottomUp = 1,
}

/// <summary>
/// Enables passive client-side blinking and the blink emotes.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BlinkingComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled = true;

    [DataField, AutoNetworkedField]
    public bool CloseOnDeath = true;

    [DataField, AutoNetworkedField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(4);

    [DataField, AutoNetworkedField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(6);

    [DataField, AutoNetworkedField]
    public TimeSpan Duration = TimeSpan.FromSeconds(0.3);

    [DataField, AutoNetworkedField]
    public TimeSpan RapidDuration = TimeSpan.FromSeconds(0.2);

    [DataField, AutoNetworkedField]
    public TimeSpan RapidInterval = TimeSpan.FromSeconds(0.35);

    [DataField, AutoNetworkedField]
    public int RapidCount = 3;

    [DataField, AutoNetworkedField]
    public BlinkingWipeDirection WipeDirection = BlinkingWipeDirection.TopDown;

    /// <summary>Reserved for non-linear eyelid wipe coverage.</summary>
    [DataField, AutoNetworkedField]
    public float CoveragePower = 3f;

    [DataField, AutoNetworkedField]
    public bool Asynchronous;

    [DataField, AutoNetworkedField]
    public ResPath? EyelidRsi;

    /// <summary>State in <see cref="EyelidRsi"/> for the screen-left eye.</summary>
    [DataField, AutoNetworkedField]
    public string? EyelidStateLeft;

    /// <summary>State in <see cref="EyelidRsi"/> for the screen-right eye.</summary>
    [DataField, AutoNetworkedField]
    public string? EyelidStateRight;

    /// <summary>
    /// Maximum delay between left/right eyes during async blinks.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan AsyncOffset = TimeSpan.FromSeconds(1);
}
