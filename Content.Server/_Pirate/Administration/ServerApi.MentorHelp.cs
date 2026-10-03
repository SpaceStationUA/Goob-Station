// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Threading.Tasks;
using Content.Server._Pirate.Administration.Systems;
using Robust.Server.ServerStatus;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    private async Task ActionSendMentorHelp(IStatusHandlerContext context)
    {
        var body = await ReadJson<BwoinkActionBody>(context);
        if (body == null)
            return;

        await RunOnMainThread(async () =>
        {
            if (!_playerManager.TryGetSessionById(new NetUserId(body.Guid), out var player))
            {
                await RespondError(
                    context,
                    ErrorCode.PlayerNotFound,
                    HttpStatusCode.UnprocessableContent,
                    "Player not found");
                return;
            }

            _entitySystemManager.GetEntitySystem<MentorHelpSystem>().OnWebhookMessage(player, body);
            await RespondOk(context);
        });
    }
}
