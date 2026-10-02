// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Goobstation.Client.VoiceChat;
using Content.Goobstation.Common.CCVar;
using Content.Pirate.Common.CCVar;
using Content.Server.Administration.Managers;
using Content.Server.Players.JobWhitelist;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;
using ServerVoiceChatManager = Content.Goobstation.Server.VoiceChat.VoiceChatManager;

namespace Content.IntegrationTests.Tests._Pirate.VoiceChat;

[TestFixture]
public sealed class VoiceAccessIntegrationTest
{
    [Test]
    public async Task VoicePolicyFollowsAdminWhitelistAndGlobalAccess()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;
        var voice = server.ResolveDependency<ServerVoiceChatManager>();
        var clientVoice = client.ResolveDependency<VoiceChatManager>();
        var admins = server.ResolveDependency<IAdminManager>();
        var whitelist = server.ResolveDependency<JobWhitelistManager>();
        var session = server.ResolveDependency<IPlayerManager>().Sessions.Single();
        var captain = new ProtoId<JobPrototype>("Captain");

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(PirateCVars.VoiceChatForAll, false);
            server.CfgMan.SetCVar(PirateCVars.VoiceChatWhitelisted, false);
            server.CfgMan.SetCVar(PirateCVars.VoiceChatAdmins, false);
            server.CfgMan.SetCVar(GoobCVars.VoiceChatWebSocketBind, "127.0.0.1:0");
            server.CfgMan.SetCVar(GoobCVars.VoiceChatEnabled, true);
            Assert.That(voice.CanUseVoice(session), Is.False);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(clientVoice.AccessAllowed, Is.False);
            Assert.That(clientVoice.RequestLink(), Is.False);
        });

        await server.WaitPost(() =>
        {
            server.CfgMan.SetCVar(PirateCVars.VoiceChatAdmins, true);
            Assert.That(admins.IsAdmin(session), Is.True);
            Assert.That(voice.CanUseVoice(session), Is.True);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() =>
        {
            Assert.That(clientVoice.AccessAllowed, Is.True);
            Assert.That(clientVoice.RequestLink(), Is.True);
        });
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.LinkCode, Is.Not.Null));

        await server.WaitPost(() => admins.DeAdmin(session));
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() => Assert.That(voice.CanUseVoice(session), Is.False));
        await client.WaitAssertion(() =>
        {
            Assert.That(clientVoice.AccessAllowed, Is.False);
            Assert.That(clientVoice.LinkCode, Is.Null);
        });

        await server.WaitPost(() => server.CfgMan.SetCVar(PirateCVars.VoiceChatWhitelisted, true));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.AccessAllowed, Is.False));

        await server.WaitPost(() => whitelist.AddWhitelist(session.UserId, captain));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.AccessAllowed, Is.True));
        await server.WaitPost(() => whitelist.RemoveWhitelist(session.UserId, captain));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.AccessAllowed, Is.False));

        await server.WaitPost(() => server.CfgMan.SetCVar(PirateCVars.VoiceChatForAll, true));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.AccessAllowed, Is.True));
        await server.WaitPost(() => server.CfgMan.SetCVar(GoobCVars.VoiceChatEnabled, false));
        await pair.RunTicksSync(5);
        await client.WaitAssertion(() => Assert.That(clientVoice.AccessAllowed, Is.False));

        await pair.CleanReturnAsync();
    }
}
