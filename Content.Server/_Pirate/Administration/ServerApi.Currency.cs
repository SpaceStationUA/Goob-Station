// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server._Pirate.ServerCurrency;
using Robust.Server.ServerStatus;

namespace Content.Server.Administration;

public sealed partial class ServerApi
{
    private async Task ActionPirateCurrency(IStatusHandlerContext context)
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
        if (!request.Valid)
        {
            await context.RespondJsonAsync(new { State = "invalid_request", Balance = (int?) null }, HttpStatusCode.BadRequest);
            return;
        }

        // The gameplay module registers currency operations after this base server handler.
        var result = await RunOnMainThread(() => IoCManager.Resolve<IPirateCurrencyOperations>().Apply(request));
        var status = result.State switch
        {
            "applied" => HttpStatusCode.OK,
            "operation_conflict" => HttpStatusCode.Conflict,
            "invalid_request" => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.UnprocessableEntity,
        };
        await context.RespondJsonAsync(new { result.State, result.Balance }, status);
    }
}
