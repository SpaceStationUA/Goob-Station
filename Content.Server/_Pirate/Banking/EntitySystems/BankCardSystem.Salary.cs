using Content.Goobstation.Common.Traits;
using Content.Server.Chat.Managers;
using Content.Shared._Pirate.Banking;
using Content.Shared.Chat;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Banking;

public sealed partial class BankCardSystem
{
    [Dependency] private readonly IChatManager _salaryChat = default!;
    [Dependency] private readonly SharedAudioSystem _salaryAudio = default!;
    [Dependency] private readonly SharedContainerSystem _salaryContainers = default!;
    [Dependency] private readonly EntityLookupSystem _salaryLookup = default!;
    [Dependency] private readonly SharedInteractionSystem _salaryInteraction = default!;

    private static readonly SoundSpecifier SalarySound = new SoundPathSpecifier(
        "/Audio/_Pirate/Banking/twobeep_high.ogg", AudioParams.Default.WithVolume(-6f));

    private void NotifySalaryPayments(HashSet<int> paidAccounts)
    {
        var cards = EntityQueryEnumerator<BankCardComponent>();
        while (cards.MoveNext(out var uid, out var card))
        {
            if (card.AccountId is not { } accountId || !paidAccounts.Contains(accountId))
                continue;

            var message = Loc.GetString("bank-card-salary-paid", ("balance", GetBalance(accountId)));
            var wrappedMessage = $"{FormattedMessage.EscapeText(Name(uid))}: {message}";
            var parent = Transform(uid).ParentUid;
            var carried = false;
            while (parent.IsValid())
            {
                if (HasComp<MobStateComponent>(parent))
                {
                    SendSalaryNotification(uid, parent, message, wrappedMessage);
                    carried = true;
                    break;
                }

                parent = Transform(parent).ParentUid;
            }

            if (carried)
                continue;

            if (_salaryContainers.TryGetContainingContainer(uid, out var container))
            {
                foreach (var occupant in container.ContainedEntities)
                {
                    SendSalaryNotification(uid, occupant, message, wrappedMessage);
                }
            }
            else
            {
                foreach (var (listener, _) in _salaryLookup.GetEntitiesInRange<ActorComponent>(
                             Transform(uid).Coordinates, 1f, LookupFlags.Uncontained))
                {
                    if (!_salaryInteraction.InRangeUnobstructed(listener, uid, range: 1f))
                        continue;

                    SendSalaryNotification(uid, listener, message, wrappedMessage);
                }
            }
        }
    }

    private void SendSalaryNotification(EntityUid card, EntityUid listener, string message, string wrappedMessage)
    {
        if (HasComp<DeafComponent>(listener) || !TryComp<ActorComponent>(listener, out var actor))
            return;

        _salaryChat.ChatMessageToOne(ChatChannel.Notifications, message, wrappedMessage, card, false,
            actor.PlayerSession.Channel);
        _salaryAudio.PlayGlobal(SalarySound, actor.PlayerSession);
    }
}
