using Content.Shared._DV.CartridgeLoader.Cartridges;

using Content.Shared._DV.NanoChat;
using Content.Shared._Pirate.NanoChat; // Pirate: nanochat monitor

namespace Content.Server._DV.CartridgeLoader.Cartridges;

public sealed partial class NanoChatCartridgeSystem : EntitySystem // Pirate: nanochat monitor
{
    public void DeliverAnonymousMessage(
        Entity<NanoChatCardComponent> recipient,
        uint senderNumber,
        string senderName,
        string content)
    {
        var message = new NanoChatMessage(0, _timing.CurTime, content, senderNumber);

        _nanoChat.SetRecipient((recipient, recipient.Comp), senderNumber,
            new NanoChatRecipient(senderNumber, senderName));

        _nanoChat.AddMessage((recipient, recipient.Comp), senderNumber, message);

        HandleUnreadNotification(recipient, message, senderNumber);

        var msgEv = new NanoChatMessageReceivedEvent(recipient);
        RaiseLocalEvent(ref msgEv);
        UpdateUIForCard(recipient);

        #region Pirate: nanochat monitor
        if (recipient.Comp.Number is not { } recipientNumber)
            return;

        var deliveredEv = new NanoChatMessageDeliveredEvent(
            ++_nextDeliveryId, // Pirate: nanochat network
            null,
            null,
            senderNumber,
            senderName,
            recipientNumber,
            [recipient.Owner],
            message);
        RaiseLocalEvent(ref deliveredEv);
        #endregion
    }
}
