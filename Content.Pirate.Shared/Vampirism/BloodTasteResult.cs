using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Vampirism;

/// <summary>
/// Result of the soft bite checks: what the blood tastes like and what it actually grants.
/// Evaluated on the first sip and carried through every repeat of the do-after.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class BloodTasteResult
{
    /// <summary>
    /// Localized taste description shown to the bloodsucker.
    /// </summary>
    [DataField]
    public string Taste = string.Empty;

    /// <summary>
    /// Whether the blood nourishes the bloodsucker (healing, hunger, thirst, blood restore).
    /// </summary>
    [DataField]
    public bool GrantsBuffs = true;

    /// <summary>
    /// Whether the blood feeds the vampire power system.
    /// </summary>
    [DataField]
    public bool GrantsPower = true;

    /// <summary>
    /// Whether the victim is drunk, so the sip leaves the bloodsucker a little drunk too.
    /// </summary>
    [DataField]
    public bool GetsDrunk;
}
