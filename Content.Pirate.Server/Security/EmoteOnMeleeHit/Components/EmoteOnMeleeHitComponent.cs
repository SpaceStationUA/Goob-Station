using Content.Server.Chat.Systems;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Prototypes;
using Content.Pirate.Server.Security.EmoteOnMeleeHit.Systems;

namespace Content.Pirate.Server.Security.EmoteOnMeleeHit.Components;

/// <summary>
/// Causes an entity to automatically emote when hit by an entity with this component.
/// </summary>
[RegisterComponent, Access(typeof(EmoteOnMeleeHitSystem)), AutoGenerateComponentPause]
public sealed partial class EmoteOnMeleeHitComponent : Component
{
    /// <summary>
    /// Chance of preforming an emote when hit by an entity with this component and not on cooldown.
    /// </summary>
    [DataField]
    public float EmoteChance = 0.5f;

    /// <summary>
    /// A set of emotes that will be randomly picked from.
    /// <see cref="EmotePrototype"/>
    /// </summary>
    // Pirate: the local engine uses typed prototype IDs instead of the removed set serializer.
    [DataField]
    public HashSet<ProtoId<EmotePrototype>> Emotes = new();

    /// <summary>
    /// Also send the emote in chat.
    /// <summary>
    [DataField]
    public bool WithChat = true;

    /// <summary>
    /// Hide the chat message from the chat window, only showing the popup.
    /// This does nothing if WithChat is false.
    /// <summary>
    [DataField]
    public bool HiddenFromChatWindow;

    /// <summary>
    /// The simulation time of the last emote preformed due to being hit by an entity with this component.
    /// </summary>
    [DataField("lastEmoteTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan LastEmoteTime = TimeSpan.Zero;

    /// <summary>
    /// The cooldown between emotes.
    /// </summary>
    [DataField]
    public TimeSpan EmoteCooldown = TimeSpan.FromSeconds(0.5);
}
