// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Server._Pirate.Chat.Systems;
using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Server.Mind;
using Content.Shared._DV.CartridgeLoader.Cartridges;
using Content.Shared._DV.NanoChat;
using Content.Shared.CartridgeLoader;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.PDA;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using ClientBwoink = Content.Client.Administration.Systems.BwoinkSystem;
using ClientMentorHelp = Content.Client._Pirate.Administration.MentorHelp.MentorHelpSystem;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
public sealed class PirateChatFilterTest
{
    [TestCase("ъ", true)]
    [TestCase("ы", true)]
    [TestCase("э", true)]
    [TestCase("Ъ", true)]
    [TestCase("Ы", true)]
    [TestCase("Э", true)]
    [TestCase("[bold]теъст[/bold]", true)]
    [TestCase("Привіт! Ґанок, їжак, єнот, ігри, м'яч.", false)]
    [TestCase("English 123 :)", false)]
    [TestCase("", false)]
    public void DetectsOnlyTheSpecifiedLetters(string text, bool blocked)
    {
        Assert.That(PirateChatFilterSystem.ContainsBlockedCharacters(text), Is.EqualTo(blocked));
    }

    [Test]
    public async Task ChatIngressRejectsBeforeBroadcastAndWarnsSender()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();
        EntityUid user = default;
        await server.WaitAssertion(() =>
        {
            user = server.EntMan.SpawnEntity("MobHuman", map.GridCoords);
            var minds = server.System<MindSystem>();
            var mind = minds.CreateMind(pair.Player!.UserId);
            minds.TransferTo(mind, user);
            server.CfgMan.SetCVar(CCVars.ChatRateLimitCount, 1000);
            server.CfgMan.SetCVar(CCVars.OocEnabled, true);
            server.CfgMan.SetCVar(CCVars.AdminOocEnabled, true);
        });
        await pair.RunTicksSync(10);
        await client.WaitAssertion(() => client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Clear());

        const string rejected = "забороненийтекстъЫэ";
        const string accepted = "Дозволенийукраїнськийтекст";
        await server.WaitAssertion(() =>
        {
            var chat = server.System<ChatSystem>();
            foreach (var type in new[] { InGameICChatType.Speak, InGameICChatType.Whisper, InGameICChatType.Emote, InGameICChatType.Telepathic, InGameICChatType.CollectiveMind })
                chat.TrySendInGameICMessage(user, rejected, type, ChatTransmitRange.Normal, player: pair.Player);
            chat.TrySendInGameICMessage(user, ";" + rejected, InGameICChatType.Speak, ChatTransmitRange.Normal, player: pair.Player);
            chat.TrySendInGameOOCMessage(user, rejected, InGameOOCChatType.Looc, false, player: pair.Player);
            chat.TrySendInGameOOCMessage(user, rejected, InGameOOCChatType.Dead, false, player: pair.Player);
            foreach (var type in new[] { OOCChatType.OOC, OOCChatType.Admin, OOCChatType.MentorChat, OOCChatType.EventChat, OOCChatType.CentComChat })
                server.ResolveDependency<IChatManager>().TrySendOOCMessage(pair.Player!, rejected, type);

            server.ResolveDependency<IChatManager>().TrySendOOCMessage(pair.Player!, accepted, OOCChatType.OOC);
            chat.TrySendInGameICMessage(user, accepted, InGameICChatType.Speak, ChatTransmitRange.Normal, player: pair.Player);
        });
        await pair.RunTicksSync(20);
        await client.WaitAssertion(() =>
        {
            var messages = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History.Select(entry => entry.Msg).ToList();
            Assert.That(messages.Any(msg => msg.Message.Contains(rejected)), Is.False);
            Assert.That(messages.Count(msg => msg.Channel == ChatChannel.Server), Is.GreaterThanOrEqualTo(13),
                "Every rejected ingress must return a server warning to the sender.");
            Assert.That(messages.Any(msg => msg.Channel == ChatChannel.OOC && msg.Message.Contains(accepted)), Is.True);
            Assert.That(messages.Any(msg => msg.Channel == ChatChannel.Local && msg.Message.Contains(accepted)), Is.True);
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NanoChatRejectsTextAndPhotoNamesBeforeCreatingMessages()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var entities = server.EntMan;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(pair.Player!, user);
            var pda = entities.SpawnEntity("PassengerPDA", map.GridCoords);
            var card = entities.SpawnEntity("IDCardStandard", map.GridCoords);
            var receiver = entities.SpawnEntity("IDCardStandard", map.GridCoords);
            var cartridge = entities.SpawnEntity("NanoChatCartridge", map.GridCoords);
            entities.GetComponent<PdaComponent>(pda).ContainedId = card;
            var sender = entities.EnsureComponent<NanoChatCardComponent>(card);
            var recipient = entities.EnsureComponent<NanoChatCardComponent>(receiver);
            var nanoChat = server.System<SharedNanoChatSystem>();
            nanoChat.SetNumber((card, sender), 100);
            nanoChat.SetNumber((receiver, recipient), 200);
            Assert.That(nanoChat.TryStorePhoto((card, sender), new NanoChatPhotoData("фотоъ.png", null)), Is.True);

            void Send(string content, string? photo = null)
            {
                CartridgeMessageEvent message = new NanoChatUiMessageEvent(NanoChatUiMessageType.SendMessage,
                    recipientNumber: 200, content: content, photoFileName: photo)
                {
                    Actor = user,
                    LoaderUid = entities.GetNetEntity(pda),
                };
                entities.EventBus.RaiseLocalEvent(cartridge, message);
            }

            Send("текстЫ");
            Send(string.Empty, "фотоъ.png");
            Assert.That(nanoChat.GetRecipients((card, sender)), Is.Empty);
            Assert.That(nanoChat.GetMessagesForRecipient((card, sender), 200), Is.Null.Or.Empty);
            Assert.That(nanoChat.GetMessagesForRecipient((receiver, recipient), 100), Is.Null.Or.Empty);

            Send("Привіт");
            Assert.That(nanoChat.GetMessagesForRecipient((card, sender), 200), Has.Count.EqualTo(1),
                "Valid Ukrainian text must still enter the sender's conversation, even with no network coverage.");
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task HelpChannelsStillDeliverQuotesContainingBlockedLetters()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var client = pair.Client;
        const string quote = "Цитата порушення: ъ ы э Ъ Ы Э";
        var ahelpReceived = false;
        var mhelpReceived = false;
        await client.WaitAssertion(() =>
        {
            var ahelp = client.System<ClientBwoink>();
            var mhelp = client.System<ClientMentorHelp>();
            ahelp.OnBwoinkTextMessageRecieved += (_, message) => ahelpReceived |= message.Text.Contains(quote);
            mhelp.MessageReceived += message => mhelpReceived |= message.Text.Contains(quote);
            ahelp.Send(pair.Player!.UserId, quote, false, false);
            mhelp.Send(pair.Player.UserId, quote, false);
        });
        await pair.RunTicksSync(30);
        await client.WaitAssertion(() =>
        {
            Assert.That(ahelpReceived, Is.True, "AHelp must remain available for quoting chat violations.");
            Assert.That(mhelpReceived, Is.True, "MHelp must remain available for quoting chat violations.");
        });
        await pair.CleanReturnAsync();
    }
}
