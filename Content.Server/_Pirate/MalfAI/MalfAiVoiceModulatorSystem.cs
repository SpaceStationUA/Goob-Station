// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT
using Content.Server.Speech;

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Goobstation.Common.Speech;
using Content.Goobstation.Common.Barks;
using Content.Shared.Access.Systems;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.Station;
using Content.Shared.Medical.SuitSensor;
using Content.Shared.Medical.SuitSensors;
using Content.Shared.Actions;
using Content.Shared._Pirate.MalfAI;
using Content.Shared.Chat;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chat.RadioIconsEvents;
using Content.Shared.CCVar;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Popups;
using Content.Shared.Roles.Jobs;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Speech;
using Content.Shared.StatusIcon;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.MalfAI;

/// <summary>
///     Server-side Malf AI voice modulator. Its state and transformations mirror
///     <see cref="Content.Server.VoiceMask.VoiceMaskSystem"/>, but the modulator
///     is intrinsic to the Malf AI rather than an item in an inventory slot.
/// </summary>
public sealed class MalfAiVoiceModulatorSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly Content.Server.Silicons.StationAi.StationAiSystem _stationAi = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IdentitySystem _identity = default!;
    [Dependency] private readonly SharedJobSystem _jobs = default!;
    [Dependency] private readonly SharedIdCardSystem _idCards = default!;
    [Dependency] private readonly SharedStationSystem _stations = default!;
    [Dependency] private readonly SharedSuitSensorSystem _sensors = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MalfAiVoiceModulatorActionEvent>(OnVoiceModulator);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, TransformSpeakerNameEvent>(OnTransformSpeakerName);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, SeeIdentityAttemptEvent>(OnSeeIdentity);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, TransformSpeechEvent>(OnTransformSpeech,
            before: [typeof(AccentSystem)]);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, GetSpeechSoundEvent>(OnGetSpeechSound);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, TransformSpeakerJobIconEvent>(OnTransformJobIcon);

        SubscribeNetworkEvent<MalfVoiceModulatorSubmitNameEvent>(OnSubmitName);
        SubscribeNetworkEvent<MalfVoiceModulatorChangeVerbEvent>(OnChangeVerb);
        SubscribeNetworkEvent<MalfVoiceModulatorChangeSoundEvent>(OnChangeSound);
        SubscribeNetworkEvent<MalfVoiceModulatorToggleEvent>(OnToggle);
        SubscribeNetworkEvent<MalfVoiceModulatorAccentToggleEvent>(OnAccentToggle);
        SubscribeNetworkEvent<MalfVoiceModulatorChangeJobIconEvent>(OnChangeJobIcon);
        SubscribeNetworkEvent<MalfVoiceModulatorCopyCrewEvent>(OnCopyCrew);
        SubscribeNetworkEvent<MalfVoiceModulatorRefreshCrewEvent>(OnRefreshCrew);
        SubscribeLocalEvent<MalfAiVoiceModulatorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnVoiceModulator(MalfAiVoiceModulatorActionEvent ev)
    {
        if (!TryGetMalfAi(ev.Performer, out var ai) || !TryComp<ActorComponent>(ai, out var actor))
            return;

        var component = EnsureComp<MalfAiVoiceModulatorComponent>(ai);
        component.CrewSnapshot = BuildCrew(ai);
        SendState(ai, actor, component);
        ev.Handled = true;
    }

    private void OnSubmitName(MalfVoiceModulatorSubmitNameEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        var newName = ev.Name.Trim();
        if (string.IsNullOrEmpty(newName) || newName.Length > _cfg.GetCVar(CCVars.MaxNameLength))
        {
            Popup(ai, "malf-voice-invalid-name", PopupType.SmallCaution);
            return;
        }

        component.VoiceName = newName;
        Dirty(ai, component);
        _identity.QueueIdentityUpdate(ai);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"AI voice modulator: {ToPrettyString(ai)} set displayed voice name to \"{newName}\"");
        Popup(ai, "malf-voice-updated");
        SendState(ai, component);
    }

    private void OnChangeVerb(MalfVoiceModulatorChangeVerbEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        if (ev.Verb is { Length: > 0 } verb && !_proto.HasIndex<SpeechVerbPrototype>(verb))
            return;

        component.SpeechVerb = ev.Verb;
        Dirty(ai, component);
        SendState(ai, component);
    }

    private void OnChangeSound(MalfVoiceModulatorChangeSoundEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        if (ev.SpeechSounds is { Length: > 0 } sounds && !_proto.HasIndex<SpeechSoundsPrototype>(sounds))
            return;

        component.SpeechSounds = ev.SpeechSounds;
        component.CopyingVoice = false;
        UpdateBark(ai, component);
        Dirty(ai, component);
        SendState(ai, component);
    }

    private void OnToggle(MalfVoiceModulatorToggleEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        component.Active = !component.Active;
        UpdateBark(ai, component);
        Dirty(ai, component);
        _identity.QueueIdentityUpdate(ai);
        SendState(ai, component);
    }

    private void OnAccentToggle(MalfVoiceModulatorAccentToggleEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        component.AccentHide = !component.AccentHide;
        Dirty(ai, component);
        SendState(ai, component);
    }

    private void OnChangeJobIcon(MalfVoiceModulatorChangeJobIconEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
            return;

        if (ev.JobIcon is not { Length: > 0 })
        {
            component.JobIconProtoId = null;
            component.JobName = null;
        }
        else if (_proto.TryIndex<JobIconPrototype>(ev.JobIcon, out var icon) && icon.AllowSelection)
        {
            component.JobIconProtoId = icon.ID;
            component.JobName = _jobs.TryFindJobFromIcon(icon, out var job) ? job.LocalizedName : null;
        }
        else
        {
            return;
        }

        Dirty(ai, component);
        SendState(ai, component);
    }

    private void OnTransformSpeakerName(Entity<MalfAiVoiceModulatorComponent> ent, ref TransformSpeakerNameEvent args)
    {
        if (!ent.Comp.Active)
            return;

        if (ent.Comp.VoiceName is { } name)
            args.VoiceName = name;
        args.SpeechVerb = ent.Comp.SpeechVerb ?? args.SpeechVerb;
    }

    private void OnSeeIdentity(Entity<MalfAiVoiceModulatorComponent> ent, ref SeeIdentityAttemptEvent args)
    {
        if (ent.Comp.Active && ent.Comp.VoiceName is { } name)
            args.NameOverride = name;
    }

    private void OnTransformSpeech(Entity<MalfAiVoiceModulatorComponent> ent, ref TransformSpeechEvent args)
    {
        if (ent.Comp.Active && ent.Comp.AccentHide)
            args.Cancel();
    }

    private void OnGetSpeechSound(Entity<MalfAiVoiceModulatorComponent> ent, ref GetSpeechSoundEvent args)
    {
        if (!ent.Comp.Active || (!ent.Comp.CopyingVoice && ent.Comp.SpeechSounds == null))
            return;

        args.SpeechSoundProtoId = ent.Comp.SpeechSounds;
        args.Handled = true;
    }

    private void OnRefreshCrew(MalfVoiceModulatorRefreshCrewEvent ev, EntitySessionEventArgs args)
    {
        if (TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component))
        {
            component.CrewSnapshot = BuildCrew(ai);
            SendState(ai, component);
        }
    }

    private bool CanCopyCrew(EntityUid ai, EntityUid target)
    {
        if (ai == target || !HasComp<HumanoidAppearanceComponent>(target) ||
            !TryComp<MindContainerComponent>(target, out var mind) || mind.Mind == null ||
            !HasComp<SpeechComponent>(target) || !_stationAi.TryGetCore(ai, out var core))
            return false;

        var station = _stations.GetOwningStation(core.Owner);
        return station != null && _stations.GetOwningStation(target) == station;
    }

    private void OnCopyCrew(MalfVoiceModulatorCopyCrewEvent ev, EntitySessionEventArgs args)
    {
        if (!TryGetMalfAi(args.SenderSession.AttachedEntity, out var ai, out var component) ||
            !TryGetEntity(ev.Target, out var target) || target is not { } crew || !CanCopyCrew(ai, crew))
            return;

        var speech = Comp<SpeechComponent>(crew);
        var sound = new GetSpeechSoundEvent();
        RaiseLocalEvent(crew, ref sound);
        component.VoiceName = Name(crew);
        component.SpeechVerb = speech.SpeechVerb;
        component.SpeechSounds = sound.Handled ? sound.SpeechSoundProtoId : speech.SpeechSounds;
        component.JobIconProtoId = "JobIconUnknown";
        component.JobName = null;
        if (_idCards.TryFindIdCard(crew, out var card))
        {
            component.JobIconProtoId = card.Comp.JobIcon;
            component.JobName = card.Comp.LocalizedJobTitle;
        }
        component.CopyingVoice = true;
        component.CopiedBark = CompOrNull<SpeechSynthesisComponent>(crew)?.VoicePrototypeId;
        component.Active = true;
        component.AccentHide = true;
        UpdateBark(ai, component);
        Dirty(ai, component);
        _identity.QueueIdentityUpdate(ai);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"AI voice modulator: {ToPrettyString(ai)} copied the voice of {ToPrettyString(crew)}");
        SendState(ai, component);
    }

    private void UpdateBark(EntityUid ai, MalfAiVoiceModulatorComponent component)
    {
        var voice = CompOrNull<SpeechSynthesisComponent>(ai);
        if (component.Active && component.CopyingVoice)
        {
            if (!component.BarkOverridden)
                component.OriginalBark = voice?.VoicePrototypeId;
            voice ??= EnsureComp<SpeechSynthesisComponent>(ai);
            voice.VoicePrototypeId = component.CopiedBark;
            component.BarkOverridden = true;
            Dirty(ai, voice);
        }
        else if (component.BarkOverridden)
        {
            if (voice != null)
            {
                voice.VoicePrototypeId = component.OriginalBark;
                Dirty(ai, voice);
            }
            component.BarkOverridden = false;
        }
    }

    private void OnShutdown(Entity<MalfAiVoiceModulatorComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.CopyingVoice = false;
        UpdateBark(ent, ent.Comp);
    }

    private void OnTransformJobIcon(Entity<MalfAiVoiceModulatorComponent> ent, ref TransformSpeakerJobIconEvent args)
    {
        if (!ent.Comp.Active || ent.Comp.JobIconProtoId is not { } icon)
            return;

        args.JobIcon = icon;
        if (!string.IsNullOrWhiteSpace(ent.Comp.JobName))
            args.JobName = ent.Comp.JobName;
    }

    private bool TryGetMalfAi(EntityUid? uid, out EntityUid ai)
    {
        ai = uid ?? EntityUid.Invalid;
        return ai != EntityUid.Invalid
               && HasComp<MalfAiMarkerComponent>(ai)
               && HasComp<StationAiHeldComponent>(ai);
    }

    private bool TryGetMalfAi(EntityUid? uid, out EntityUid ai, [NotNullWhen(true)] out MalfAiVoiceModulatorComponent? component)
    {
        ai = uid ?? EntityUid.Invalid;
        if (ai == EntityUid.Invalid || !HasComp<MalfAiMarkerComponent>(ai) || !HasComp<StationAiHeldComponent>(ai))
        {
            component = null;
            return false;
        }

        return TryComp(ai, out component);
    }

    private void Popup(EntityUid ai, string message, PopupType type = PopupType.Small)
    {
        var target = GetAiEyeForPopup(ai) ?? ai;
        _popup.PopupEntity(Loc.GetString(message), target, ai, type);
    }

    private void SendState(EntityUid ai, ActorComponent actor, MalfAiVoiceModulatorComponent component)
    {
        if (actor.PlayerSession != null)
            RaiseNetworkEvent(new MalfVoiceModulatorOpenUiEvent(BuildState(ai, component)), Filter.SinglePlayer(actor.PlayerSession));
    }

    private void SendState(EntityUid ai, MalfAiVoiceModulatorComponent component)
    {
        if (TryComp<ActorComponent>(ai, out var actor))
            SendState(ai, actor, component);
    }

    private MalfVoiceModulatorState BuildState(EntityUid ai, MalfAiVoiceModulatorComponent component)
    {
        var verbs = new List<MalfVoiceModulatorOption> { new(string.Empty, Loc.GetString("chat-speech-verb-name-none")) };
        foreach (var proto in _proto.EnumeratePrototypes<SpeechVerbPrototype>().OrderBy(p => Loc.GetString(p.Name)))
            verbs.Add(new(proto.ID, Loc.GetString(proto.Name)));

        var sounds = new List<MalfVoiceModulatorOption> { new(string.Empty, Loc.GetString("malf-voice-sound-none")) };
        foreach (var proto in _proto.EnumeratePrototypes<SpeechSoundsPrototype>().OrderBy(p => p.ID))
            sounds.Add(new(proto.ID, proto.ID));

        var jobIcons = new List<MalfVoiceModulatorOption> { new(string.Empty, Loc.GetString("malf-voice-job-none")) };
        foreach (var proto in _proto.EnumeratePrototypes<JobIconPrototype>()
                     .Where(p => p.AllowSelection)
                     .OrderBy(p => p.LocalizedJobName))
            jobIcons.Add(new(proto.ID, proto.LocalizedJobName));

        return new MalfVoiceModulatorState(
            component.VoiceName ?? Name(ai),
            component.SpeechVerb,
            component.SpeechSounds,
            component.Active,
            component.AccentHide,
            component.JobIconProtoId,
            verbs,
            sounds,
            jobIcons,
            component.CrewSnapshot);
    }

    private List<MalfVoiceModulatorCrewMember> BuildCrew(EntityUid ai)
    {
        var crew = new List<MalfVoiceModulatorCrewMember>();
        // Only explicit UI refreshes sample sensors. Never expose health without coordinate mode.
        var health = new Dictionary<NetEntity, MalfVoiceCrewHealth>();
        var sensors = EntityQueryEnumerator<SuitSensorComponent, TransformComponent>();
        while (sensors.MoveNext(out var sensorUid, out var sensor, out var xform))
        {
            if (sensor.Mode != SuitSensorMode.SensorCords || sensor.User is not { } wearer || !CanCopyCrew(ai, wearer))
                continue;
            var status = _sensors.GetSensorState((sensorUid, sensor, xform));
            if (status?.Coordinates == null)
                continue;
            health[status.OwnerUid] = !status.IsAlive ? MalfVoiceCrewHealth.Dead
                : status.IsCritical ? MalfVoiceCrewHealth.Critical
                : status.TotalDamage is > 0 ? MalfVoiceCrewHealth.Wounded
                : MalfVoiceCrewHealth.Healthy;
        }
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent, MindContainerComponent, SpeechComponent>();
        while (query.MoveNext(out var uid, out _, out _, out _))
        {
            if (!CanCopyCrew(ai, uid))
                continue;
            var hasCard = _idCards.TryFindIdCard(uid, out var card);
            var job = hasCard ? card.Comp.LocalizedJobTitle : null;
            string jobIcon = hasCard ? card.Comp.JobIcon : "JobIconUnknown";
            var net = GetNetEntity(uid);
            crew.Add(new(net, Name(uid), job ?? string.Empty,
                jobIcon, health.GetValueOrDefault(net)));
        }
        return crew.OrderBy(member => member.Name).ToList();
    }

    private EntityUid? GetAiEyeForPopup(EntityUid aiUid)
    {
        if (!_stationAi.TryGetCore(aiUid, out var core) || core.Comp?.RemoteEntity == null)
            return null;

        return core.Comp.RemoteEntity.Value;
    }
}
