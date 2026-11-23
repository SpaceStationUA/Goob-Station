// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.Json;
using System.Linq;
using Content.Pirate.Server.Discord;
using Content.Shared.Mind;
using Content.Server.GameTicking;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Replays;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._Pirate.GameTicking;

[TestFixture]
public sealed class RoundReportTest
{
    [Test]
    public async Task PurchasedMageAndFinalScoreboardWorkWithoutRecording()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = false, Connected = true, InLobby = true, Dirty = true });
        var server = pair.Server;
        var ticker = server.ResolveDependency<IEntityManager>().System<GameTicker>();
        var data = server.ResolveDependency<IResourceManager>().UserData;
        var replays = server.ResolveDependency<IReplayRecordingManager>();
        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(20);
        await server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            Assert.That(replays.IsRecording, Is.False);
            var session = server.PlayerMan.Sessions.Single();
            var original = session.AttachedEntity;
            Assert.That(original, Is.Not.Null);
            Assert.That(server.System<PirateShopSystem>().TryMakeMage(session), Is.True);
            Assert.That(server.System<SharedMindSystem>().TryGetMind(session.UserId, out _, out var mind), Is.True);
            var mage = mind!.CurrentEntity!.Value;
            Assert.That(mage, Is.Not.EqualTo(original));
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(mage).EntityPrototype?.ID, Is.EqualTo("MobHumanMage"));
            Assert.That(session.AttachedEntity, Is.EqualTo(mage));
            var dir = new ResPath("/round-reports");
            data.CreateDir(dir);
            try
            {
                ticker.EndRound("Round report test");
                using var report = JsonDocument.Parse(data.ReadAllText(dir / $"{ticker.RoundId}.json"));
                var root = report.RootElement;
                Assert.That(root.GetProperty("roundId").GetInt32(), Is.EqualTo(ticker.RoundId));
                Assert.That(root.GetProperty("roundEndText").GetString(), Does.Contain("Round report test"));
                Assert.That(root.GetProperty("roundEndPlayers").ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(root.GetProperty("pirateShiftPhotos").ValueKind, Is.EqualTo(JsonValueKind.Array));
                Assert.That(data.Exists(dir / $"{ticker.RoundId}.tmp"), Is.False);
            }
            finally
            {
                data.Delete(dir);
            }
        });
        await pair.CleanReturnAsync();
    }
}
