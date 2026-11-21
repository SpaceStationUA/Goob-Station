// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.UserInterface.Systems.Chat;
using Content.Pirate.Shared;
using Content.Pirate.Shared.Components;
using Content.Server.Chat.Systems;
using Content.Server.Ghost.Roles;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.Emoting;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests.Minds;

[TestFixture]
public sealed class TargetedGhostTests
{
    public enum ControlEnd
    {
        Timeout,
        TargetDeleted,
        AdminBodyDeleted,
        AdminReassigned,
    }

    [TestCase(false, ControlEnd.Timeout)]
    [TestCase(true, ControlEnd.Timeout)]
    [TestCase(true, ControlEnd.TargetDeleted)]
    [TestCase(true, ControlEnd.AdminBodyDeleted)]
    [TestCase(true, ControlEnd.AdminReassigned)]
    public async Task SecondSpiritTakesOverAndReturnsTheOriginalOwner(bool visiting, ControlEnd ending)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var entities = server.EntMan;
        EntityUid target = default, owner = default, second = default, secondMind = default, adminBody = default;

        await server.WaitAssertion(() =>
        {
            var minds = server.System<SharedMindSystem>();
            var roles = server.System<GhostRoleSystem>();
            target = entities.SpawnEntity("MobDeathSkeleton", map.GridCoords);
            owner = minds.CreateMind(null);
            minds.TransferTo(owner, target);

            (EntityUid Spirit, EntityUid Mind) Possess(bool alreadyInvulnerable = false)
            {
                var spirit = entities.SpawnEntity(visiting ? "AdminObserver" : "MobDeathSkeleton", map.GridCoords);
                var mind = minds.CreateMind(null);
                if (visiting)
                {
                    adminBody = entities.SpawnEntity("MobHuman", map.GridCoords);
                    minds.TransferTo(mind, adminBody);
                    minds.Visit(mind, spirit);
                }
                else
                    minds.TransferTo(mind, spirit);

                if (alreadyInvulnerable)
                    entities.EnsureComponent<GodmodeComponent>(spirit);

                var form = entities.EnsureComponent<GhostTargetingComponent>(spirit);
                entities.EventBus.RaiseLocalEvent(spirit, new ToggleGhostFormActionEvent { Performer = spirit });
                form.Target = entities.GetNetEntity(target);
                var takeover = new TakeControlGhostActionEvent { Performer = spirit };
                entities.EventBus.RaiseLocalEvent(spirit, takeover);
                Assert.That(takeover.Handled, Is.True);
                Assert.That(minds.TryGetMind(target, out var current, out _), Is.True);
                Assert.That(current, Is.EqualTo(mind.Owner));
                Assert.That(roles.GhostRoles.Any(role => role.Owner == target), Is.False);
                return (spirit, mind.Owner);
            }

            var first = Possess();
            (second, secondMind) = Possess(alreadyInvulnerable: true);
            Assert.That(minds.TryGetMind(first.Spirit, out var returned, out _), Is.True);
            Assert.That(returned, Is.EqualTo(first.Mind));
            Assert.That(roles.GhostRoles.Any(role => role.Owner == first.Spirit), Is.False);

            Assert.That(entities.HasComponent<GodmodeComponent>(first.Spirit), Is.True);
            entities.RemoveComponent<GhostTargetingComponent>(first.Spirit);
            Assert.That(entities.HasComponent<GodmodeComponent>(first.Spirit), Is.False);
            if (visiting)
                Assert.That(entities.HasComponent<EmotingComponent>(first.Spirit), Is.False);
            if (ending == ControlEnd.AdminBodyDeleted)
                entities.DeleteEntity(adminBody);
            if (ending == ControlEnd.AdminReassigned)
            {
                adminBody = entities.SpawnEntity("MobHuman", map.GridCoords);
                minds.TransferTo(secondMind, adminBody, ghostCheckOverride: true);
            }

            if (ending == ControlEnd.TargetDeleted)
                entities.DeleteEntity(target);
            else
                entities.GetComponent<GhostTargetingComponent>(second).ControlTimeEnd = TimeSpan.Zero;
        });

        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            if (ending != ControlEnd.TargetDeleted)
            {
                Assert.That(server.System<SharedMindSystem>().TryGetMind(target, out var returned, out _), Is.True);
                Assert.That(returned, Is.EqualTo(owner));
            }

            if (visiting)
            {
                var adminMind = entities.GetComponent<MindComponent>(secondMind);
                if (ending == ControlEnd.AdminBodyDeleted)
                {
                    Assert.That(adminMind.OwnedEntity, Is.EqualTo(second));
                    Assert.That(adminMind.VisitingEntity, Is.Null);
                }
                else
                {
                    Assert.That(adminMind.OwnedEntity, Is.EqualTo(adminBody));
                    EntityUid? expectedVisit = ending == ControlEnd.AdminReassigned ? null : second;
                    Assert.That(adminMind.VisitingEntity, Is.EqualTo(expectedVisit));
                }
            }

            Assert.That(server.System<GhostRoleSystem>().GhostRoles.Any(role => role.Owner == target), Is.False);
            if (ending == ControlEnd.AdminReassigned)
                Assert.That(entities.EntityExists(second), Is.False);
            else
            {
                entities.RemoveComponent<GhostTargetingComponent>(second);
                Assert.That(entities.HasComponent<GodmodeComponent>(second), Is.True);
            }
        });
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task OnlySelectedPlayerReceivesObserverSpeechAndActions()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        var client = pair.Client;
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var player = entities.SpawnEntity("MobHuman", map.GridCoords);
            var minds = server.System<SharedMindSystem>();
            minds.TransferTo(minds.CreateMind(pair.Player!.UserId), player);
            var spirit = entities.SpawnEntity("AdminObserver", map.GridCoords);
            var form = entities.EnsureComponent<GhostTargetingComponent>(spirit);
            entities.EventBus.RaiseLocalEvent(spirit, new ToggleGhostFormActionEvent { Performer = spirit });
            var chat = server.System<ChatSystem>();
            chat.TrySendInGameICMessage(spirit, "unselectedgesture", InGameICChatType.Emote, ChatTransmitRange.Normal);
            form.Target = entities.GetNetEntity(player);
            chat.TrySendInGameICMessage(spirit, "privatebracket [", InGameICChatType.Speak, ChatTransmitRange.Normal);
            chat.TrySendInGameICMessage(spirit, "privategesture", InGameICChatType.Emote, ChatTransmitRange.Normal);
            Assert.That(chat.TryEmoteWithChat(spirit, "Clap", nameOverride: "privatewheel"), Is.True);
            chat.TrySendInGameICMessage(spirit, "hiddenline", InGameICChatType.Speak, ChatTransmitRange.HideChat);
            entities.EventBus.RaiseLocalEvent(spirit, new ToggleGhostFormActionEvent { Performer = spirit });
            Assert.That(entities.HasComponent<EmotingComponent>(spirit), Is.False);
        });
        await pair.RunTicksSync(15);
        await client.WaitAssertion(() =>
        {
            var messages = client.ResolveDependency<IUserInterfaceManager>().GetUIController<ChatUIController>().History
                .Select(entry => entry.Msg).ToArray();
            bool Contains(string text, string marker) => text.Contains(marker, StringComparison.OrdinalIgnoreCase);
            Assert.That(messages.Any(m => Contains(m.Message, "unselectedgesture")), Is.False);
            Assert.That(messages.Any(m => m.Channel == ChatChannel.Local && Contains(m.Message, "privatebracket")), Is.True);
            Assert.That(messages.Any(m => m.Channel == ChatChannel.Emotes && Contains(m.Message, "privategesture")), Is.True);
            Assert.That(messages.Any(m => m.Channel == ChatChannel.Emotes && Contains(m.WrappedMessage, "privatewheel")), Is.True);
            Assert.That(messages.Any(m => Contains(m.Message, "hiddenline")), Is.False);
        });
        await pair.CleanReturnAsync();
    }
}
