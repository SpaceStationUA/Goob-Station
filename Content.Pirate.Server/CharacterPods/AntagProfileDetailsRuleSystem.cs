// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Shared.Humanoid;

namespace Content.Pirate.Server.CharacterPods;

public sealed class AntagProfileDetailsRuleSystem : EntitySystem
{
    [Dependency] private readonly CharacterProfileSpawnSystem _profileSpawn = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AntagProfileDetailsRuleComponent, AfterAntagEntitySelectedEvent>(OnAntagSelected);
    }

    private void OnAntagSelected(Entity<AntagProfileDetailsRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        if (args.Session is not { } session)
            return;

        if (!_profileSpawn.TryGetSelectedProfile(session, out var profile))
            return;

        if (HasComp<HumanoidAppearanceComponent>(args.EntityUid))
            _metaData.SetEntityName(args.EntityUid, profile.Name);

        _profileSpawn.ApplyProfileDetails(args.EntityUid, profile, session);
    }
}
