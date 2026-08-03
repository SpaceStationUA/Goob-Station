using Robust.Shared.GameStates;

namespace Content.Shared._DV.Psionics.Components.PsionicPowers;

/// <summary>
///     How the forced words are delivered.
/// </summary>
public enum ForceSpeechType
{
    /// <summary>
    ///     The entity speaks the words through the say channel ("X says: ...").
    /// </summary>
    Say,

    /// <summary>
    ///     The words are delivered as an emote ("*X ...*").
    /// </summary>
    Emote,
}

/// <summary>
///     Forces the entity to speak a set of words whenever a psionic power action
///     on the same entity is used successfully.
///     Attach this to a psionic power action prototype (next to its action event)
///     or next to a <see cref="BasePsionicPowerComponent"/> - the
///     <see cref="Content.Server._DV.Psionics.Systems.ForceSpeechSystem"/> handles the rest.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ForceSpeechComponent : Component
{
    /// <summary>
    ///     The locale string of the words the entity is forced to speak.
    ///     Can use the "entity" parameter (the performer of the power).
    /// </summary>
    [DataField]
    public LocId SpeechText;

    /// <summary>
    ///     How the words are delivered.
    /// </summary>
    [DataField]
    public ForceSpeechType SpeechType = ForceSpeechType.Say;
}
