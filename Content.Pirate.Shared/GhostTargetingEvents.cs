using Content.Shared.Actions;
using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared
{

    public sealed partial class ClearTargetGhostActionEvent : InstantActionEvent {}

    public sealed partial class SetTargetGhostActionEvent : EntityTargetActionEvent {}

    public sealed partial class ToggleGhostFormActionEvent : InstantActionEvent {}

    public sealed partial class GhostBlinkActionEvent : InstantActionEvent { }

    /// <summary>Lets the current possessor return the body before another one takes over.</summary>
    [ByRefEvent]
    public readonly record struct RelinquishBodyControlEvent;

    [Serializable, NetSerializable]
    public sealed class TargetedGhostEmoteMessage(NetEntity spirit, ProtoId<EmotePrototype> emote) : EntityEventArgs
    {
        public NetEntity Spirit = spirit;
        public ProtoId<EmotePrototype> Emote = emote;
    }

    public sealed partial class TakeControlGhostActionEvent : InstantActionEvent { }

}

