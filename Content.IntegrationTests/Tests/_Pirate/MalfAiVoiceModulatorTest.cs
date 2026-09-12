// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Speech;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;
using System.Linq;
using Content.Server.Mind;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Inventory;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Players;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiVoiceModulatorTest
{
    [Test]
    public async Task CrewSelectionCopiesVoiceAndOnlyShowsHealthWithCoordinates()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            DummyTicker = false,
            Dirty = true,
        });
        var server = pair.Server;
        var entMan = server.EntMan;
        var map = await pair.CreateTestMap();
        EntityUid ai = default;
        EntityUid human = default;
        EntityUid uniform = default;
        NetEntity target = default;
        await server.WaitAssertion(() =>
        {
            var station = entMan.SpawnEntity("MalfRemoteTestStation", MapCoordinates.Nullspace);
            entMan.System<StationSystem>().AddGridToStation(station, map.Grid);
            var core = entMan.SpawnEntity("PlayerStationAiEmpty", map.GridCoords);
            entMan.System<SharedPowerReceiverSystem>().SetNeedsPower(core, false);
            ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            var containers = entMan.System<SharedContainerSystem>();
            Assert.That(containers.Insert(ai, containers.GetContainer(core, StationAiHolderComponent.Container)), Is.True);
            var minds = entMan.System<MindSystem>();
            var session = server.PlayerMan.Sessions.Single();
            minds.TransferTo(session.ContentData()!.Mind!.Value, ai, ghostCheckOverride: true);
            server.PlayerMan.SetAttachedEntity(session, ai);
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);

            human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(minds.CreateMind(null, "Crew voice"), human);
            var speech = entMan.GetComponent<SpeechComponent>(human);
            speech.SpeechVerb = "Robotic";
            speech.SpeechSounds = "Borg";
            var card = entMan.SpawnEntity("PassengerIDCard", map.GridCoords);
            entMan.GetComponent<IdCardComponent>(card).JobIcon = "JobIconCaptain";
            var inventory = entMan.System<InventorySystem>();
            uniform = entMan.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
            Assert.That(inventory.TryEquip(human, uniform, "jumpsuit"), Is.True);
            // The ID slot depends on wearing a jumpsuit.
            Assert.That(inventory.TryEquip(human, card, "id"), Is.True);
            entMan.System<SharedSuitSensorSystem>().SetSensor(
                (uniform, entMan.GetComponent<SuitSensorComponent>(uniform)), SuitSensorMode.SensorVitals, human);
            target = entMan.GetNetEntity(human);
            entMan.EventBus.RaiseLocalEvent(ai, new MalfAiVoiceModulatorActionEvent { Performer = ai });
            Assert.That(entMan.GetComponent<MalfAiVoiceModulatorComponent>(ai).CrewSnapshot.Single().Health,
                Is.EqualTo(MalfVoiceCrewHealth.Unknown));
        });
        await pair.RunTicksSync(5);
        await pair.Client.WaitPost(() => pair.Client.EntMan.EntityNetManager.SendSystemNetworkMessage(new MalfVoiceModulatorCopyCrewEvent(target)));
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            var voice = entMan.GetComponent<MalfAiVoiceModulatorComponent>(ai);
            Assert.Multiple(() =>
            {
                Assert.That(voice.VoiceName, Is.EqualTo(entMan.GetComponent<MetaDataComponent>(human).EntityName));
                Assert.That(voice.SpeechVerb?.ToString(), Is.EqualTo("Robotic"));
                Assert.That(voice.SpeechSounds?.ToString(), Is.EqualTo("Borg"));
                Assert.That(voice.JobIconProtoId?.ToString(), Is.EqualTo("JobIconCaptain"));
                Assert.That(voice.CopyingVoice, Is.True);
            });
            entMan.System<SharedSuitSensorSystem>().SetSensor(
                (uniform, entMan.GetComponent<SuitSensorComponent>(uniform)), SuitSensorMode.SensorCords, human);
        });
        await pair.Client.WaitPost(() => pair.Client.EntMan.EntityNetManager.SendSystemNetworkMessage(new MalfVoiceModulatorRefreshCrewEvent()));
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(
            entMan.GetComponent<MalfAiVoiceModulatorComponent>(ai).CrewSnapshot.Single().Health,
            Is.EqualTo(MalfVoiceCrewHealth.Healthy)));
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VoiceModulatorTransformsIdentityVerbSoundAndAccentLikeVoiceMask()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid ai = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            entMan.EnsureComponent<MalfAiMarkerComponent>(ai);
            entMan.EnsureComponent<StationAiHeldComponent>(ai);
            var voice = entMan.EnsureComponent<MalfAiVoiceModulatorComponent>(ai);
            voice.VoiceName = "Central Command";
            voice.SpeechVerb = "Robotic";
            voice.SpeechSounds = "Borg";
            voice.Active = true;
            voice.AccentHide = true;

            var name = new TransformSpeakerNameEvent(ai, "Original AI");
            entMan.EventBus.RaiseLocalEvent(ai, name);
            Assert.That(name.VoiceName, Is.EqualTo("Central Command"));
            Assert.That(name.SpeechVerb, Is.EqualTo("Robotic"));

            voice.VoiceName = null;
            var originalName = new TransformSpeakerNameEvent(ai, "Original AI");
            entMan.EventBus.RaiseLocalEvent(ai, originalName);
            Assert.That(originalName.VoiceName, Is.EqualTo("Original AI"));
            Assert.That(originalName.SpeechVerb, Is.EqualTo("Robotic"),
                "Selecting a speech style must not require renaming the AI first.");

            var sound = new GetSpeechSoundEvent();
            entMan.EventBus.RaiseLocalEvent(ai, ref sound);
            Assert.Multiple(() =>
            {
                Assert.That(sound.Handled, Is.True);
                Assert.That(sound.SpeechSoundProtoId, Is.EqualTo("Borg"));
            });

            var speech = new TransformSpeechEvent(ai, "accented text");
            entMan.EventBus.RaiseLocalEvent(ai, speech);
            Assert.That(speech.Cancelled, Is.True);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task VoiceModulatorToggleRestoresOriginalVoicePath()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.EntMan;
        EntityUid ai = default;

        await server.WaitAssertion(() =>
        {
            ai = entMan.SpawnEntity(null, map.GridCoords.Offset(new Vector2(1, 0)));
            var voice = entMan.EnsureComponent<MalfAiVoiceModulatorComponent>(ai);
            voice.VoiceName = "Masked Name";
            voice.SpeechVerb = "Robotic";
            voice.SpeechSounds = "Borg";
            voice.Active = false;
            voice.AccentHide = true;

            var name = new TransformSpeakerNameEvent(ai, "Original AI");
            entMan.EventBus.RaiseLocalEvent(ai, name);
            Assert.That(name.VoiceName, Is.EqualTo("Original AI"));
            Assert.That(name.SpeechVerb, Is.Null);

            var sound = new GetSpeechSoundEvent();
            entMan.EventBus.RaiseLocalEvent(ai, ref sound);
            Assert.That(sound.Handled, Is.False);

            var speech = new TransformSpeechEvent(ai, "accented text");
            entMan.EventBus.RaiseLocalEvent(ai, speech);
            Assert.That(speech.Cancelled, Is.False);
        });

        await pair.CleanReturnAsync();
    }
}
