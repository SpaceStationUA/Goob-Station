using Content.Server.Players.PlayTimeTracking;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared._Pirate.Origin;
using Content.Shared.Customization.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Humanoid;
using Content.Shared.Players;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using LanguageSystem = Content.Server._EinsteinEngines.Language.LanguageSystem;

namespace Content.Pirate.Server.Origin.Systems;

public sealed class CitizenshipSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null || !_prototype.TryIndex<JobPrototype>(args.JobId, out _))
            return;

        ApplyCitizenship(args.Mob, args.Profile, args.Player);
    }

    public void ApplyCitizenship(EntityUid uid, HumanoidCharacterProfile profile, ICommonSession session)
    {
        if (!_playTimeTracking.TryGetTrackerTimes(session, out var playTimes))
            playTimes = new Dictionary<string, TimeSpan>();

        ApplyCitizenship(uid, profile, playTimes);
    }

    public void ApplyCitizenship(EntityUid uid, HumanoidCharacterProfile profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        ProtoId<CitizenshipPrototype> citizenship = string.IsNullOrEmpty(profile.Citizenship)
        ? SharedHumanoidAppearanceSystem.DefaultCitizenship
        : profile.Citizenship;

        if (!_prototype.TryIndex<CitizenshipPrototype>(citizenship, out var citizenshipPrototype)
            && !_prototype.TryIndex<CitizenshipPrototype>(SharedHumanoidAppearanceSystem.DefaultCitizenship, out citizenshipPrototype))
        {
            DebugTools.Assert(false, $"Citizenship '{citizenship}' and the default citizenship were not found!");
            return;
        }

        if (!RequirementsMet(citizenshipPrototype.Requirements, profile, playTimes))
            return;

        foreach (var language in citizenshipPrototype.Languages)
        {
            if (_prototype.TryIndex<LanguagePrototype>(language, out _))
                _language.AddLanguage(uid, language);
            else
                DebugTools.Assert(false, $"Language '{language}' configured for citizenship '{citizenshipPrototype.ID}' was not found!");
        }
    }

    private bool RequirementsMet(
        IReadOnlyCollection<JobRequirement> requirements,
        HumanoidCharacterProfile profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes)
    {
        foreach (var requirement in requirements)
        {
            if (!requirement.Check(EntityManager, _prototype, profile, playTimes, out _))
                return false;
        }

        return true;
    }
}
