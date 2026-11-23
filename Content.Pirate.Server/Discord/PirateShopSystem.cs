// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Goobstation.Common.ServerCurrency;
using Content.Pirate.Server.Mercenary;
using Content.Server._Pirate.Ghost.Roles;
using Content.Server._Pirate.ServerCurrency;
using Content.Server.Administration.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Ghost.Roles.Components;
using Content.Shared.Ghost;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mind;
using Content.Shared.Movement.Components;
using Content.Shared.Players;
using Content.Shared.Revenant.Components;
using Content.Shared.Roles;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.Discord;

public sealed class PirateShopSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IBanManager _bans = default!;
    [Dependency] private readonly GameTicker _ticker = default!;
    [Dependency] private readonly MakeAMercSystem _merc = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly GhostRoleLoadoutSystem _loadouts = default!;
    [Dependency] private readonly SharedMapSystem _maps = default!;

    // Keep the delivery outcome only while the database has not acknowledged settlement.
    private readonly Dictionary<Guid, string> _unsettled = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<PirateShopEvent>(OnPurchase);
    }

    private void OnPurchase(PirateShopEvent ev)
    {
        var request = ev.Request;
        var rejection = request.InspectOnly ? null : Eligible(request, out _, out _);
        var hold = Task.Run(() => _db.ReservePirateShop(request, rejection)).GetAwaiter().GetResult();
        if (hold.State != "shop_held")
        {
            _unsettled.Remove(request.OperationId);
            ev.Result = hold;
            return;
        }

        if (!_unsettled.TryGetValue(request.OperationId, out var outcome))
        {
            // A hold left by a previous process had no surviving game-world delivery.
            outcome = "unfulfilled";
            if (hold.AppliedNow)
            {
                outcome = Eligible(request, out var session, out var body);
                if (outcome == null)
                {
                    try
                    {
                        outcome = Deliver(request.Item, session!, body) ? "delivered" : "unfulfilled";
                    }
                    catch (Exception e)
                    {
                        Log.Error($"Delivering shop operation {request.OperationId} ({request.Item}): {e}");
                        outcome = "unfulfilled";
                    }
                }
            }
            _unsettled.Add(request.OperationId, outcome);
        }

        var settled = Task.Run(() => _db.SettlePirateShop(request, outcome)).GetAwaiter().GetResult();
        _unsettled.Remove(request.OperationId);
        ev.Result = settled;
        if (settled.AppliedNow && settled.Balance is {} balance &&
            _players.TryGetSessionById(new NetUserId(request.Source), out var current) && current.Channel.IsConnected)
            RaiseNetworkEvent(new PlayerBalanceUpdateEvent(balance, balance), current);
    }

    private string? Eligible(PirateShopRequest request, out ICommonSession? session, out EntityUid body)
    {
        session = null;
        body = default;
        if (_ticker.RunLevel != GameRunLevel.InRound || _ticker.RoundId != request.RoundId)
            return "stale_round";
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= request.ExpiresAt)
            return "expired";
        if (!_players.TryGetSessionById(new NetUserId(request.Source), out session) ||
            session.Status != SessionStatus.InGame || !session.Channel.IsConnected ||
            session.AttachedEntity is not {} attached || TerminatingOrDeleted(attached) || session.ContentData() == null)
            return "offline";

        body = attached;
        if (HasComp<RevenantComponent>(attached))
            return "ineligible";

        if (request.Item is "mercenary" or "magespawn")
        {
            var mage = request.Item == "magespawn";
            if (_players.PlayerCount < (mage ? 20 : 5) || mage && _ticker.RoundDuration() < TimeSpan.FromMinutes(10))
                return "ineligible";
            List<ProtoId<AntagPrototype>> roles = [mage ? "Wizard" : "Mercenary"];
            return _bans.IsRoleBanned(session, roles) ? "ineligible" : null;
        }
        return HasComp<GhostComponent>(attached) ? "ineligible" : null;
    }

    private bool Deliver(string item, ICommonSession session, EntityUid body)
    {
        switch (item)
        {
            case "mercenary":
                return _merc.TryMakeMercenary(body) is {} merc && _minds.TryGetMind(session.UserId, out _, out var mind)
                    && mind.CurrentEntity == merc;
            case "magespawn":
                return TryMakeMage(session);
            case "catears":
                SpawnAttachedTo("ClothingHeadHatCatEars", new EntityCoordinates(body, 0, 0));
                return true;
            case "rgblight":
                EnsureComp<RgbLightControllerComponent>(body);
                EnsureComp<PointLightComponent>(body);
                return true;
            case "sparkles":
                EnsureComp<ActiveJetpackComponent>(body);
                return true;
            default:
                return false;
        }
    }

    public bool TryMakeMage(ICommonSession session)
    {
        if (session.AttachedEntity is not {} source || session.ContentData() == null || !session.Channel.IsConnected)
            return false;

        var rule = _ticker.AddGameRule("MageSpawn");
        if (!TryComp<RuleGridsComponent>(rule, out var grids) || grids.Map is not {} map)
        {
            QueueDel(rule);
            return false;
        }

        var granted = false;
        try
        {
            var query = EntityQueryEnumerator<GhostRoleMobSpawnerComponent, TransformComponent>();
            while (query.MoveNext(out var marker, out var spawner, out var transform))
            {
                if (transform.MapID != map || Prototype(marker)?.ID != "SpawnPointGhostMage" || spawner.Prototype == null)
                    continue;

                if (!session.Channel.IsConnected || session.AttachedEntity != source)
                    return false;
                var mage = SpawnAtPosition(spawner.Prototype, transform.Coordinates);
                // Consume only the marker on this purchase's map before another player can claim it.
                RemComp<GhostRoleComponent>(marker);
                QueueDel(marker);
                var mind = session.ContentData()!.Mind ?? _minds.CreateMind(session.UserId, MetaData(source).EntityName);
                _minds.TransferTo(mind, mage, true);
                granted = _minds.TryGetMind(session.UserId, out _, out var current) && current.CurrentEntity == mage;
                if (granted)
                    _loadouts.EquipLoadout(mage, session, marker);
                return granted;
            }
            return false;
        }
        catch (Exception e) when (granted)
        {
            Log.Error($"Unable to equip optional loadout on the delivered mage: {e}");
            return true;
        }
        finally
        {
            if (!granted)
            {
                _maps.DeleteMap(map);
                QueueDel(rule);
            }
        }
    }
}
