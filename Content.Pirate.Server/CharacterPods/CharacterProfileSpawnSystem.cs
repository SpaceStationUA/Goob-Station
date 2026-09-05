// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Contractors.Systems;
using Content.Server._Pirate.Character.Info;
using Content.Server._Pirate.Traits;
using Content.Server.Humanoid;
using Content.Server.CharacterAppearance.Components;
using Content.Server.Preferences.Managers;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;

namespace Content.Pirate.Server.CharacterPods;

public sealed class CharacterProfileSpawnSystem : EntitySystem
{
    [Dependency] private readonly HumanoidAppearanceSystem _humanoid = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly NationalitySystem _nationality = default!;
    [Dependency] private readonly PirateCharacterInfoSystem _characterInfo = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly TraitSystem _traits = default!;

    public bool TryGetSelectedProfile(ICommonSession session, out HumanoidCharacterProfile profile)
    {
        profile = default!;

        if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs)
            || prefs.SelectedCharacter is not HumanoidCharacterProfile selected)
        {
            return false;
        }

        profile = selected;
        return true;
    }

    public bool IsSpeciesAllowed(RandomHumanoidSettingsPrototype settings, string species)
    {
        if (!_prototype.HasIndex<SpeciesPrototype>(species))
            return false;

        if (settings.SpeciesWhitelist != null)
            return settings.SpeciesWhitelist == species;

        return !settings.SpeciesBlacklist.Contains(species);
    }

    public HumanoidCharacterProfile RollProfile(RandomHumanoidSettingsPrototype settings)
    {
        return settings.SpeciesWhitelist != null
            ? HumanoidCharacterProfile.RandomWithSpecies(settings.SpeciesWhitelist)
            : HumanoidCharacterProfile.Random(settings.SpeciesBlacklist);
    }

    public EntityUid SpawnFromProfile(RandomHumanoidSettingsPrototype settings, HumanoidCharacterProfile profile,
        EntityCoordinates coordinates, string? nameOverride = null)
    {
        var species = _prototype.Index<SpeciesPrototype>(profile.Species);
        var body = EntityManager.CreateEntityUninitialized(species.Prototype, coordinates);

        _metaData.SetEntityName(body, settings.RandomizeName || nameOverride == null ? profile.Name : nameOverride);

        if (settings.Components != null)
        {
            foreach (var entry in settings.Components.Values)
            {
                var comp = (Component) _serialization.CreateCopy(entry.Component, notNullableOverride: true);
                RemComp(body, comp.GetType());
                AddComp(body, comp);
            }
        }

        RemComp<RandomHumanoidAppearanceComponent>(body);

        EntityManager.InitializeAndStartEntity(body);

        _humanoid.LoadProfile(body, profile);

        return body;
    }

    public void ApplyProfileDetails(EntityUid mob, HumanoidCharacterProfile profile, ICommonSession session)
    {
        ApplySkills(mob, profile);
        _traits.ApplyProfileTraits(mob, profile, session, null);
        _characterInfo.ApplyCharacterInfo(mob, profile);
        _nationality.ApplyNationality(mob, profile, session);
    }

    private void ApplySkills(EntityUid mob, HumanoidCharacterProfile profile)
    {
        var speciesId = CompOrNull<HumanoidAppearanceComponent>(mob)?.Species ?? profile.Species;

        if (!_prototype.TryIndex<SpeciesPrototype>(speciesId, out var species))
            return;

        _knowledge.ApplyProfile(mob, species.Knowledge, profile.Knowledge);
        _knowledge.ApplyEmployerBonuses(mob, profile.Employer);
    }
}
