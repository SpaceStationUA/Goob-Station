// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Common.ServerCurrency;
using Content.Server.Destructible;
using Content.Server.Destructible.Thresholds.Behaviors;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.ServerCurrency;

[DataDefinition]
public sealed partial class GiveGoobcoinsToKillerBehaviour : IThresholdBehavior
{
    [DataField]
    public int Amount = 1;

    public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
    {
        if (cause is not {} killer || killer == owner)
            return;

        if (!system.EntityManager.TryGetComponent(killer, out ActorComponent? actor))
            return;

        var userId = actor.PlayerSession.UserId;
        try
        {
            IoCManager.Resolve<ICommonCurrencyManager>().AddCurrency(userId, Amount);
        }
        catch (Exception ex)
        {
            // Pirate: a failed reward must not interrupt the remaining destruction behaviors.
            Logger.GetSawmill("server_currency").Error($"Could not award {Amount} currency to {userId} for destroying {owner}: {ex}");
        }
    }
}
