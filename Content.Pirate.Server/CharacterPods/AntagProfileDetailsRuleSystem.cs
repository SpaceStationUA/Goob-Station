// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.Antag;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules;
using Content.Shared.Humanoid;

namespace Content.Pirate.Server.CharacterPods;

public sealed class AntagProfileDetailsRuleSystem : EntitySystem
{
    [Dependency] private readonly CharacterProfileSpawnSystem _profileSpawn = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Mind creation and the round summary copy the entity name.
        SubscribeLocalEvent<AntagProfileDetailsRuleComponent, AntagSelectEntityEvent>(
            OnSelectEntity,
            after: [typeof(AntagLoadProfileRuleSystem)]);

        // Profile skills clear the knowledge store, so they must apply before antag skill packages.
        SubscribeLocalEvent<AntagProfileDetailsRuleComponent, AfterAntagEntitySelectedEvent>(
            OnAntagSelected,
            before: [typeof(AntagPlayerEffectsSystem)]);
    }

    private void OnSelectEntity(Entity<AntagProfileDetailsRuleComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Entity is not { } body
            || args.Session is not { } session
            || !HasComp<HumanoidAppearanceComponent>(body)
            || !_profileSpawn.TryGetSelectedProfile(session, out var profile))
        {
            return;
        }

        _metaData.SetEntityName(body, profile.Name);
    }

    private void OnAntagSelected(Entity<AntagProfileDetailsRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is not { } session)
            return;

        if (!_profileSpawn.TryGetSelectedProfile(session, out var profile))
            return;

        _profileSpawn.ApplyProfileDetails(args.EntityUid, profile, session);
    }
}
