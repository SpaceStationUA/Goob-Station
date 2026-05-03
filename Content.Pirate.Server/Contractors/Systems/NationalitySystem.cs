using System.Linq;
using Content.Server.Players.PlayTimeTracking;
using Content.Shared._Pirate.Contractors.Prototypes;
using Content.Shared.Customization.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Utility;

namespace Content.Pirate.Server.Contractors.Systems;

public sealed class NationalitySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly Content.Server._EinsteinEngines.Language.LanguageSystem _language = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add the nationality components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args) =>
        ApplyNationality(args.Mob, args.JobId, args.Profile,
            _playTimeTracking.GetTrackerTimes(args.Player));

    /// <summary>
    ///     Adds the nationality selected by a player to an entity.
    /// </summary>
    public void ApplyNationality(EntityUid uid, ProtoId<JobPrototype>? jobId, HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes)
    {
        if (jobId == null || !_prototype.TryIndex(jobId.Value, out var jobPrototypeToUse))
            return;

        ProtoId<NationalityPrototype> nationality = string.IsNullOrEmpty(profile.Nationality)
        ? SharedHumanoidAppearanceSystem.DefaultNationality
        : profile.Nationality;

        if(!_prototype.TryIndex<NationalityPrototype>(nationality, out var nationalityPrototype))
        {
            DebugTools.Assert($"Nationality '{nationality}' not found!");
            return;
        }

        if (!RequirementsMet(nationalityPrototype.Requirements, profile, playTimes))
            return;

        AddNationality(uid, nationalityPrototype);
        GrantNationalityLanguages(uid, nationality);
    }

    /// <summary>
    ///     Adds a single Nationality Prototype to an Entity.
    /// </summary>
    public void AddNationality(EntityUid uid, NationalityPrototype nationalityPrototype)
    {
        // Prototype application functions were removed; no-op for now.
    }
    private bool RequirementsMet(List<JobRequirement> requirements, HumanoidCharacterProfile profile, IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        if (requirements == null || requirements.Count == 0)
            return true;

        foreach (var requirement in requirements)
        {
            if (!requirement.Check(EntityManager, _prototype, profile, playTimes, out _))
                return false;
        }

        return true;
    }

    private void GrantNationalityLanguages(EntityUid uid, ProtoId<NationalityPrototype> nationality)
    {
    if (!_prototype.TryIndex(nationality, out var nationalityProto))
        return;

    foreach (var language in nationalityProto.Languages)
    {
        _language.AddLanguage(uid, language);
    }
    }
}
