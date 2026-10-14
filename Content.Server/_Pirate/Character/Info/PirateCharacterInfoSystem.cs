// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Character.Info;
using Content.Shared._Pirate.Character.Info.Components;
using Content.Shared.Examine;
using Content.Shared.Humanoid;
using Content.Shared.IdentityManagement;
using Content.Shared.Mind;
using Content.Shared.Roles.Jobs;
using Robust.Shared.Configuration;
using CoreCCVars = Content.Shared.CCVar.CCVars;

namespace Content.Server._Pirate.Character.Info;

public sealed class PirateCharacterInfoSystem : PirateSharedCharacterInfoSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RequestCharacterInspectInfoEvent>(OnInspectInfoRequest);
    }

    private void OnInspectInfoRequest(RequestCharacterInspectInfoEvent ev, EntitySessionEventArgs args)
    {
        // Match the inspect verb's visibility checks.
        if (args.SenderSession.AttachedEntity is not { } viewer
            || !TryGetEntity(ev.Target, out var target)
            || !_cfg.GetCVar(PirateVars.CharacterInspectWindowEnabled)
            || !HasComp<HumanoidAppearanceComponent>(target)
            || Identity.Name(target.Value, EntityManager) != MetaData(target.Value).EntityName
            || !_examineSystem.IsInDetailsRange(viewer, target.Value))
        {
            return;
        }

        var mind = _mindSystem.GetMind(target.Value);
        var job = _jobs.MindTryGetJobName(mind, out var jobName) ? jobName : null;

        string? personality = null;
        if (mind != null
            && _cfg.GetCVar(CoreCCVars.FlavorText)
            && TryComp(mind.Value, out CharacterDescriptionComponent? description))
        {
            personality = description.Description;
        }

        string? oocNotes = null;
        if (mind != null
            && _cfg.GetCVar(PirateVars.OOCNotes)
            && TryComp(mind.Value, out RoleplayInfoComponent? roleplay))
        {
            oocNotes = roleplay.OOCNotes;
        }

        RaiseNetworkEvent(new CharacterInspectInfoEvent(ev.Target, job, personality, oocNotes), args.SenderSession);
    }
}
