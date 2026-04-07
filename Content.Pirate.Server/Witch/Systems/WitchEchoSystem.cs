using Content.Server.Chat.Systems;
using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Chat;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchEchoSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    private readonly Dictionary<EntityUid, int> _suppressed = [];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchEchoComponent, EntitySpokeEvent>(OnSpoke);
    }

    private void OnSpoke(Entity<WitchEchoComponent> ent, ref EntitySpokeEvent args)
    {
        if (args.Source != ent.Owner || args.Channel != null)
            return;

        if (_suppressed.GetValueOrDefault(ent.Owner) > 0)
            return;

        var baseType = args.IsWhisper ? InGameICChatType.Whisper : InGameICChatType.Speak;
        var originalMessage = args.Message;
        var totalRepeats = ent.Comp.Repeats;

        for (var i = 0; i < totalRepeats; i++)
        {
            var echoIndex = i;
            var delay = TimeSpan.FromSeconds(ent.Comp.DelaySeconds * (i + 1));
            Timer.Spawn(delay, () => SendEcho(ent.Owner, originalMessage, baseType, echoIndex, totalRepeats));
        }
    }

    private void SendEcho(EntityUid uid, string message, InGameICChatType baseType, int echoIndex, int totalRepeats)
    {
        if (!Exists(uid) || Deleted(uid) || Terminating(uid))
            return;

        _suppressed[uid] = _suppressed.GetValueOrDefault(uid) + 1;

        try
        {
            var chatType = echoIndex >= 1 ? InGameICChatType.Whisper : baseType;
            var echoMessage = TransformEchoMessage(message, echoIndex, totalRepeats);
            _chat.TrySendInGameICMessage(uid, echoMessage, chatType, false, hideLog: true, ignoreActionBlocker: true, forced: true);
        }
        finally
        {
            var left = _suppressed.GetValueOrDefault(uid) - 1;
            if (left <= 0)
                _suppressed.Remove(uid);
            else
                _suppressed[uid] = left;
        }
    }

    private static string TransformEchoMessage(string message, int echoIndex, int totalRepeats)
    {
        var transformed = message.TrimEnd();

        transformed = TrimExcitement(transformed, echoIndex + 1);

        if (echoIndex >= Math.Max(1, totalRepeats - 2))
        {
            transformed = transformed.TrimEnd('.', '!', '?', ' ');
            transformed += "...";
        }

        return transformed;
    }

    private static string TrimExcitement(string message, int steps)
    {
        var current = message;

        for (var i = 0; i < steps; i++)
        {
            if (current.EndsWith("!!"))
            {
                current = current[..^1].TrimEnd();
                continue;
            }

            if (current.EndsWith('!') || current.EndsWith('?'))
            {
                current = current[..^1].TrimEnd();
                continue;
            }
        }

        return current;
    }
}
