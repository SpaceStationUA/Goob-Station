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

public sealed class LifepathSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    [Dependency] private readonly PlayTimeTrackingManager _playTimeTracking = default!;
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
    }

    // When the player is spawned in, add the Lifepath components selected during character creation
    private void OnPlayerSpawnComplete(PlayerSpawnCompleteEvent args) =>
        ApplyLifepath(args.Mob, args.JobId, args.Profile,
            _playTimeTracking.GetTrackerTimes(args.Player));

    /// <summary>
    ///     Adds the Lifepath selected by a player to an entity.
    /// </summary>
    public void ApplyLifepath(EntityUid uid, ProtoId<JobPrototype>? jobId, HumanoidCharacterProfile profile,
        Dictionary<string, TimeSpan> playTimes)
    {
        if (jobId == null || !_prototype.TryIndex(jobId, out _))
            return;

        var jobPrototypeToUse = _prototype.Index(jobId.Value);

        ProtoId<LifepathPrototype> lifepath = profile.Lifepath != string.Empty? profile.Lifepath : SharedHumanoidAppearanceSystem.DefaultLifepath;

        if(!_prototype.TryIndex<LifepathPrototype>(lifepath, out var lifepathPrototype))
        {
            DebugTools.Assert($"Lifepath '{lifepath}' not found!");
            return;
        }

        if (!RequirementsMet(lifepathPrototype.Requirements, profile, playTimes))
            return;

        AddLifepath(uid, lifepathPrototype);
    }

    /// <summary>
    ///     Adds a single Lifepath Prototype to an Entity.
    /// </summary>
    public void AddLifepath(EntityUid uid, LifepathPrototype lifepathPrototype)
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
}
