namespace Content.Pirate.Client.Blinking;

/// <summary>
/// Client-only runtime state for blink scheduling and eyelid animation.
/// Per-eye arrays use index 0 for left/unified and index 1 for right async eyelids.
/// </summary>
[RegisterComponent]
public sealed partial class BlinkingVisualsComponent : Component
{
    public const string UnifiedKey = "PirateBlinkEyelid";

    public const string LeftKey = "PirateBlinkEyelidL";

    public const string RightKey = "PirateBlinkEyelidR";

    public bool Async;

    public Color EyelidColor = Color.Black;

    public float MasterDelay;

    public int RapidEventsLeft;

    public bool RapidActive;

    public bool[] Pending = new bool[2];

    public float[] StartDelay = new float[2];

    public float[] Progress = { -1f, -1f };

    public float[] CurrentDuration = new float[2];
}
