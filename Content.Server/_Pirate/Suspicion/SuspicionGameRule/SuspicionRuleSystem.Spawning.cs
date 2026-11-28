// SPDX-FileCopyrightText: Suspicion on Space Station contributors
// SPDX-License-Identifier: AGPL-3.0-only

using System.Linq;
using Content.Server.Administration.Commands;
using Content.Server.Atmos.Components;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Shared.Radio.Components;
using Content.Server.Roles;
using Content.Server.Temperature.Components;
using Content.Server.Traits.Assorted;
using Content.Shared._Pirate.Suspicion;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule;
using Content.Shared._Pirate.Suspicion.SuspicionGameRule.Components;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Atmos.Rotting;
using Content.Goobstation.Common.Gibbing;
using Robust.Shared.Random;
using Content.Shared.GameTicking;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mind;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
using Content.Shared.Players;
using Content.Shared.Security.Components;
using Robust.Shared.Prototypes;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Ghost;
using Content.Shared.Mobs;
using Content.Shared.Temperature.Components;
using Robust.Shared.Audio;
using Robust.Shared.Enums;

namespace Content.Server._Pirate.Suspicion.SuspicionGameRule;

public sealed partial class SuspicionRuleSystem
{
    private static readonly EntProtoId MarkerPrototype = "SSSGridMarker";

    private void OnGetBriefing(Entity<SuspicionRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Briefing = role.Comp.Role switch
        {
            SuspicionRole.Traitor => Loc.GetString("roles-antag-suspicion-traitor-objective"),
            SuspicionRole.Detective => Loc.GetString("roles-antag-suspicion-detective-objective"),
            SuspicionRole.Innocent => Loc.GetString("roles-antag-suspicion-innocent-objective"),
            SuspicionRole.Wildcard => role.Comp.SubRole switch
            {
                SuspicionSubRole.Jester => Loc.GetString("roles-antag-suspicion-jester-objective"),
                _ => Loc.GetString("roles-antag-suspicion-pending-objective"),
            },
            _ => Loc.GetString("roles-antag-suspicion-pending-objective")
        };
    }

    private void StartRound(SuspicionRuleComponent component)
    {
        // The station may have been preloaded before the rule started.
        var apcs = EntityQueryEnumerator<ApcComponent, TransformComponent>();
        while (apcs.MoveNext(out _, out _, out var transform))
            Spawn(MarkerPrototype, transform.Coordinates);

        var participatingPlayers = new List<(EntityUid mind, SuspicionRoleComponent comp)>();
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                !_mindSystem.TryGetMind(session.UserId, out var mindId, out var mind) ||
                mind.OwnedEntity is not {} body || !HasComp<SuspicionPlayerComponent>(body) ||
                !_roleSystem.MindHasRole<SuspicionRoleComponent>(mindId.Value, out var role))
                continue;

            var returningGhost = session.AttachedEntity != body;
            if (returningGhost && (!_mobState.IsDead(body) || mind.VisitingEntity != session.AttachedEntity ||
                    !TryComp<GhostComponent>(session.AttachedEntity, out var ghost) || !ghost.CanReturnToBody))
                continue;

            // Everyone gets a fresh start when roles are dealt, including a player killed during preparation.
            _rejuvenate.PerformRejuvenate(body);
            if (returningGhost)
                _mindSystem.UnVisit(mindId.Value, mind);
            participatingPlayers.Add((mindId.Value, role.Value.Comp2));
        }

        if (participatingPlayers.Count < 2)
        {
            StopClock();
            component.GameState = SuspicionGameState.PostRound;
            _chatManager.DispatchServerAnnouncement(Loc.GetString("pirate-ttt-too-few"));
            _roundEndSystem.EndRound(TimeSpan.FromSeconds(5));
            return;
        }

        component.GameState = SuspicionGameState.InProgress;
        component.PostRoundDuration = Math.Max(0, _postRoundDuration);
        component.EndAt = Timing.CurTime + TimeSpan.FromSeconds(_roundDuration);

        // At least one opponent must remain, including in a two-player duel.
        var count = participatingPlayers.Count;
        var traitorCount = Math.Clamp((int)(count * _traitorPercentage), 1, count - 1);
        var detectiveCount = Math.Clamp((int)(count * _detectivePercentage), 1, count - traitorCount);
        var wildcardCount = RobustRandom.Prob(_wildcardChance)
            ? Math.Min(Math.Max(1, (int)(count * _wildcardPercentage)), count - traitorCount - detectiveCount)
            : 0;

        for (var i = 0; i < traitorCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];
            role.comp.Role = SuspicionRole.Traitor;
            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            AddKeyToRadio(ownedEntity.Value, component.TraitorRadio);

            _npcFactionSystem.AddFaction(ownedEntity.Value, component.TraitorFaction);

            _subdermalImplant.AddImplants(ownedEntity.Value, [component.UplinkImplant]);

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                Loc.GetString("pirate-ttt-briefing-traitor"),
                Color.Red,
                _traitorStartSound);

            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Traitor), ownedEntity.Value);
            participatingPlayers.Remove(role);
        }

        for (var i = 0; i < detectiveCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];
            role.comp.Role = SuspicionRole.Detective;
            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            EnsureComp<CriminalRecordComponent>(ownedEntity.Value).StatusIcon = "SecurityIconDischarged";

            AddKeyToRadio(ownedEntity.Value, component.DetectiveRadio);

            _subdermalImplant.AddImplants(ownedEntity.Value, [component.DetectiveImplant]);

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                Loc.GetString("pirate-ttt-briefing-detective"),
                Color.LightBlue,
                briefingSound: null);
            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Detective), ownedEntity.Value);
            participatingPlayers.Remove(role);
        }

        var wildcardRoles = new List<SuspicionSubRole>
        {
            SuspicionSubRole.Jester
        };

        for (var i = 0; i < wildcardCount; i++)
        {
            var role = participatingPlayers[RobustRandom.Next(participatingPlayers.Count)];

            var selectedSubRole = wildcardRoles[RobustRandom.Next(wildcardRoles.Count)];

            role.comp.Role = SuspicionRole.Wildcard;
            role.comp.SubRole = selectedSubRole;

            string briefingText = selectedSubRole switch
            {
                SuspicionSubRole.Jester => Loc.GetString("pirate-ttt-briefing-jester"),
                _ => Loc.GetString("pirate-ttt-briefing-wildcard")
            };

            SoundPathSpecifier? briefingSound = selectedSubRole switch
            {
                SuspicionSubRole.Jester => new SoundPathSpecifier("/Audio/Voice/Cluwne/cluwnelaugh1.ogg"),
                _ => null
            };

            var ownedEntity = Comp<MindComponent>(role.mind).OwnedEntity;
            if (!ownedEntity.HasValue)
            {
                Log.Error("Player mind has no entity.");
                continue;
            }

            switch (selectedSubRole)
            {
                case SuspicionSubRole.Jester:
                    {
                        EnsureComp<PacifiedComponent>(ownedEntity.Value);
                        break;
                    }
            }

            _antagSelectionSystem.SendBriefing(
                ownedEntity.Value,
                briefingText,
                Color.LightPink,
                briefingSound: briefingSound);

            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Wildcard, selectedSubRole), ownedEntity.Value);

            participatingPlayers.Remove(role);
        }

        // Anyone who isn't a traitor will get the innocent role.
        foreach (var (mind, role) in participatingPlayers)
        {
            var ownedEntity = Comp<MindComponent>(mind).OwnedEntity;
            if (role.Role != SuspicionRole.Pending || !ownedEntity.HasValue)
                continue;

            AssignInnocent(ownedEntity.Value, role);
            RaiseNetworkEvent(new SuspicionRuleUpdateRole(SuspicionRole.Innocent), ownedEntity.Value);
        }

        SendAllyMarkersToViewers();
        _chatManager.DispatchServerAnnouncement(Loc.GetString("pirate-ttt-start", ("count", traitorCount)));

        RaiseNetworkEvent(new SuspicionRuleTimerUpdate(component.EndAt));
    }

    private void AssignInnocent(EntityUid body, SuspicionRoleComponent role)
    {
        role.Role = SuspicionRole.Innocent;
        _antagSelectionSystem.SendBriefing(
            body,
            Loc.GetString("pirate-ttt-briefing-innocent"),
            briefingColor: Color.Green,
            briefingSound: null);
    }

    private void OnBeforeSpawn(PlayerBeforeSpawnEvent ev)
    {
        if (ev.Handled || ActiveRule is not { } active)
            return;

        var sus = active.Comp;
        if (sus.GameState != SuspicionGameState.Preparing)
        {
            Log.Debug("Player tried to join a game of Suspicion but the game is not in the preparing state.");
            _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("pirate-ttt-latejoin"));
            GameTicker.SpawnObserver(ev.Player); // Players can't join mid-round.
            ev.Handled = true;
            RaiseNetworkEvent(new SuspicionRulePlayerSpawn
            {
                GameState = sus.GameState,
                EndTime = sus.EndAt,
            }, ev.Player);
            return;
        }

        RaiseNetworkEvent(new SuspicionRulePlayerSpawn()
        {
            GameState = SuspicionGameState.Preparing,
            EndTime = sus.EndAt,
        }, ev.Player);

        var mobMaybe = _stationSpawningSystem.SpawnPlayerCharacterOnStation(ev.Station, null, ev.Profile, ckey: ev.Player.Name);
        if (mobMaybe is not {} mob)
            return;

        var newMind = _mindSystem.CreateMind(ev.Player.UserId, ev.Profile.Name);

        _mindSystem.TransferTo(newMind, mob);
        _outfits.SetOutfit(mob, sus.Gear);
        EquipArenaBreathingGear(mob);
        _roleSystem.MindAddRole(newMind, "MindRoleSuspicion");

        // Rounds only last like 5 minutes, so players shouldn't need to eat or drink.
        RemComp<ThirstComponent>(mob);
        RemComp<HungerComponent>(mob);
        RemComp<TemperatureSpeedComponent>(mob);

        EnsureComp<ShowCriminalRecordIconsComponent>(mob); // Hijacking criminal records for the blue "D" symbol.

        // Because of the limited tools available to crew, we need to make sure that spacings are not lethal.
        if (TryComp<BarotraumaComponent>(mob, out var pressureDamage))
            pressureDamage.MaxDamage = 90;
        if (TryComp<TemperatureDamageComponent>(mob, out var temperatureDamage))
            temperatureDamage.ColdDamageThreshold = float.MinValue;

        EnsureComp<IntrinsicRadioTransmitterComponent>(mob);
        var allAccess = _prototypeManager.EnumeratePrototypes<AccessLevelPrototype>().Select(p => new ProtoId<AccessLevelPrototype>(p.ID)).ToArray();
        _accessSystem.TrySetTags(mob, allAccess, EnsureComp<AccessComponent>(mob));

        EnsureComp<SuspicionPlayerComponent>(mob);

        RemComp<PerishableComponent>(mob);
        RemComp<RottingComponent>(mob); // No rotting bodies in this mode.

        _killTracking.SetKillState(mob, MobState.Dead);
        EnsureComp<DamageGibImmuneComponent>(mob); // Examination is important.

        if (_idCardSystem.TryFindIdCard(mob, out var idCard))
        {
            idCard.Comp.FullName = MetaData(mob).EntityName;
            idCard.Comp.LocalizedJobTitle = Loc.GetString("job-name-psychologist");
        }

        ev.Handled = true;
    }
}
