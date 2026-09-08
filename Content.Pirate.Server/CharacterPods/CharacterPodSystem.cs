// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Ghost.Roles;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost.Roles.Events;
using Content.Server.Popups;
using Content.Shared.Ghost.Roles.Components;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Preferences;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.CharacterPods;

public sealed class CharacterPodSystem : EntitySystem
{
    [Dependency] private readonly CharacterProfileSpawnSystem _profileSpawn = default!;
    [Dependency] private readonly GhostRoleSystem _ghostRole = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CharacterPodComponent, GhostRoleTakeAttemptEvent>(OnTakeAttempt);
        SubscribeLocalEvent<CharacterPodComponent, TakeGhostRoleEvent>(OnTakeGhostRole);
    }

    private void OnTakeAttempt(Entity<CharacterPodComponent> ent, ref GhostRoleTakeAttemptEvent args)
    {
        if (args.Cancelled || !TryGetSettings(ent, out var settings) || !IsSpeciesRejected(settings, args.Player))
            return;

        args.Cancelled = true;
        RejectPlayer(args.Player);
    }

    private void OnTakeGhostRole(Entity<CharacterPodComponent> ent, ref TakeGhostRoleEvent args)
    {
        if (args.TookRole || args.Cancelled)
            return;

        if (!TryComp<GhostRoleComponent>(ent, out var ghostRole) || ghostRole.Taken || MetaData(ent).EntityPaused)
            return;

        if (!TryGetSettings(ent, out var settings))
            return;

        if (IsSpeciesRejected(settings, args.Player))
        {
            RejectPlayer(args.Player);
            return;
        }

        var usesCharacter = _profileSpawn.TryGetSelectedProfile(args.Player, out var profile);
        if (!usesCharacter)
        {
            Log.Warning($"No character available for {args.Player.Name} taking {ToPrettyString(ent)}, rolling a body.");
            profile = _profileSpawn.RollProfile(settings);
        }

        var mob = _profileSpawn.SpawnFromProfile(settings, profile, Transform(ent).Coordinates);
        _transform.AttachToGridOrMap(mob);

        RemComp<GhostRoleComponent>(mob);
        RemComp<GhostTakeoverAvailableComponent>(mob);

        if (ent.Comp.ExtraComponents.Count > 0)
            EntityManager.AddComponents(mob, ent.Comp.ExtraComponents);

        var spawnedEvent = new GhostRoleSpawnerUsedEvent(ent, mob);
        RaiseLocalEvent(mob, spawnedEvent, true);

        if (ghostRole.MakeSentient)
            _mind.MakeSentient(mob, ghostRole.AllowMovement, ghostRole.AllowSpeech);

        EnsureComp<MindContainerComponent>(mob);

        _ghostRole.GhostRoleInternalCreateMindAndTransfer(args.Player, ent, mob, ghostRole);

        if (usesCharacter)
            _profileSpawn.ApplyProfileDetails(mob, profile, args.Player);

        args.TookRole = true;

        if (++ent.Comp.CurrentTakeovers < ent.Comp.AvailableTakeovers)
            return;

        _ghostRole.SetTaken(ghostRole, true);

        if (ent.Comp.DeleteOnSpawn)
            QueueDel(ent);
    }

    private bool TryGetSettings(Entity<CharacterPodComponent> ent, out RandomHumanoidSettingsPrototype settings)
    {
        if (_prototype.TryIndex(ent.Comp.Settings, out var indexed))
        {
            settings = indexed;
            return true;
        }

        settings = default!;
        Log.Error($"Character pod {ToPrettyString(ent)} has no valid role settings '{ent.Comp.Settings}'.");
        return false;
    }

    private bool IsSpeciesRejected(RandomHumanoidSettingsPrototype settings, ICommonSession player)
    {
        return _profileSpawn.TryGetSelectedProfile(player, out var profile)
               && !_profileSpawn.IsSpeciesAllowed(settings, profile.Species);
    }

    private void RejectPlayer(ICommonSession player)
    {
        _popup.PopupCursor(Loc.GetString("character-pod-species-not-allowed"), player);
    }
}
