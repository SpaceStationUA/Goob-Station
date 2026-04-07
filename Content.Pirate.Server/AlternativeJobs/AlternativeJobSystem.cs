using Content.Pirate.Common.AlternativeJobs;
using Content.Server.Access.Components;
using Content.Server.CrewManifest;
using Content.Server.Chat.Managers;
using Content.Shared._Shitcode._Pirate.AlternativeJobs;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.StatusIcon;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Player;

namespace Content.Pirate.Server.AlternativeJobs;

public sealed class AlternativeJobSystem : EntitySystem, IAlternativeJobSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AlternativeJobComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(EntityUid uid, AlternativeJobComponent component, PlayerSpawnCompleteEvent args)
    {
        // If no job is set, ignore
        if (args.JobId is null) return;
        if (args.Profile is null) return;
        // Check player for idCard
        var idCardSystem = EntityManager.System<SharedIdCardSystem>();
        if (!idCardSystem.TryFindIdCard(uid, out var idCard)) return;
        // Check card for component just in case
        if (!TryComp<IdCardComponent>(idCard, out var idCardComp)) return;
        if (!idCardComp.Initialized)
            return;

        // Get alternative job proto based on player preferences and job
        if (!TryGetAlternativeJob(args.JobId, args.Profile, out var alternativeJobPrototype)) return;
        if (TryComp<PresetIdCardComponent>(idCard, out var presetIdCardComp)) { presetIdCardComp.JobName = alternativeJobPrototype.LocalizedJobName; }
        var newIcon = _prototypeManager.Index<JobIconPrototype>(alternativeJobPrototype.JobIconProtoId ?? idCardComp.JobIcon);
        idCardSystem.TryChangeJobTitle(idCard, alternativeJobPrototype.LocalizedJobName, idCardComp);
        idCardComp.JobIcon = newIcon.ID;
        idCardComp.LocalizedJobTitle = alternativeJobPrototype.LocalizedJobName;
        idCardComp.JobPrototype = alternativeJobPrototype.ParentJobId;
        // Change job title on id card
        Dirty(idCard, idCardComp);

        // Notify the player about the alternative job name
        if (TryComp<ActorComponent>(uid, out var actor))
        {
            if (_prototypeManager.TryIndex<JobPrototype>(alternativeJobPrototype.ParentJobId, out var parentJobProto))
            {
                _chat.DispatchServerMessage(actor.PlayerSession, Loc.GetString("alternative-job-notify", ("newJobName", alternativeJobPrototype.LocalizedJobName), ("parentJobName", parentJobProto.LocalizedName)));
            }
        }
    }

    public bool TryGetAlternativeJob(string parentJobId, HumanoidCharacterProfile profile, out AlternativeJobPrototype alternativeJobPrototype)
    {
        if (profile.JobAlternatives.TryGetValue(parentJobId, out var alternativeJobId))
        {
            if (_prototypeManager.TryIndex(alternativeJobId, out var altJobPrototype))
            {
                alternativeJobPrototype = altJobPrototype;
                return true;
            }
        }

        alternativeJobPrototype = default!;
        return false;
    }
}
