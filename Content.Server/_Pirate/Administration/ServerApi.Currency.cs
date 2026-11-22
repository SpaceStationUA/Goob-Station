// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Pirate.ServerCurrency;
using Content.Server.Chat.Managers;
using Robust.Server.ServerStatus;
using Robust.Shared.Network;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    [Dependency] private readonly IChatManager _pirateChat = default!;

    private async Task ActionPirateCurrency(IStatusHandlerContext context, bool thanksReward)
    {
        PirateCurrencyRequest request;
        try
        {
            request = await context.RequestBodyJsonAsync<PirateCurrencyRequest>();
        }
        catch (JsonException)
        {
            await context.RespondJsonAsync(new { State = "invalid_request", Balance = (int?) null }, HttpStatusCode.BadRequest);
            return;
        }
        if (!request.Valid || (request.Kind == PirateCurrencyRequest.ThanksReward) != thanksReward)
        {
            await context.RespondJsonAsync(new { State = "invalid_request", Balance = (int?) null }, HttpStatusCode.BadRequest);
            return;
        }

        // The gameplay module registers currency operations after this base server handler.
        var result = await RunOnMainThread(() =>
        {
            var applied = IoCManager.Resolve<IPirateCurrencyOperations>().Apply(request);
            var recipient = thanksReward ? request.Source : request.Target;
            if (applied.AppliedNow && recipient is {} id && _playerManager.TryGetSessionById(new NetUserId(id), out var session))
            {
                var key = thanksReward ? "pirate-discord-thanks-reward" : "pirate-discord-currency-gift";
                _pirateChat.DispatchServerMessage(session, Loc.GetString(key, ("amount", request.Amount)), suppressLog: true);
            }
            return applied;
        });
        var status = result.State switch
        {
            "applied" => HttpStatusCode.OK,
            "operation_conflict" => HttpStatusCode.Conflict,
            "invalid_request" => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.UnprocessableEntity,
        };
        await context.RespondJsonAsync(new { result.State, result.Balance }, status);
    }

    private sealed record PirateThanksMessage(Guid Recipient, int Round, string Author, string Text);

    private async Task ActionPirateThanksMessage(IStatusHandlerContext context)
    {
        var body = await ReadJson<PirateThanksMessage>(context);
        if (body == null)
            return;

        if (body.Recipient == Guid.Empty || body.Round <= 0 || string.IsNullOrWhiteSpace(body.Author) || body.Author.Length > 160
            || string.IsNullOrWhiteSpace(body.Text) || body.Text.Length > 700)
        {
            await RespondBadRequest(context, "Invalid round thanks message");
            return;
        }

        var delivered = await RunOnMainThread(() =>
        {
            if (!_playerManager.TryGetSessionById(new NetUserId(body.Recipient), out var player))
                return false;

            // DispatchServerMessage escapes markup and sends to this session only.
            _pirateChat.DispatchServerMessage(player, Loc.GetString("pirate-discord-round-thanks",
                ("round", body.Round.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                ("author", body.Author.ReplaceLineEndings(" ")), ("text", body.Text.ReplaceLineEndings(" "))), suppressLog: true);
            return true;
        });
        await context.RespondJsonAsync(new { Delivered = delivered });
    }
}
