// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.Speech;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using System.Numerics;

namespace Content.IntegrationTests.Tests._Pirate;

[TestFixture]
public sealed class MalfAiVoiceModulatorTest
{
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
