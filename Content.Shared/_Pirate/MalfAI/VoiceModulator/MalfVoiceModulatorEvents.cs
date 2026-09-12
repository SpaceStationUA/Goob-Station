// SPDX-FileCopyrightText: 2025 Tyranex <bobthezombie4@gmail.com>
//
// SPDX-License-Identifier: MIT

using System;
using System.Collections.Generic;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.MalfAI;

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorOpenUiEvent : EntityEventArgs
{
    public MalfVoiceModulatorState State { get; }

    public MalfVoiceModulatorOpenUiEvent(MalfVoiceModulatorState state)
    {
        State = state;
    }
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorState
{
    public string Name { get; }
    public string? Verb { get; }
    public string? SpeechSounds { get; }
    public bool Active { get; }
    public bool AccentHide { get; }
    public string? JobIcon { get; }
    public List<MalfVoiceModulatorOption> Verbs { get; }
    public List<MalfVoiceModulatorOption> Sounds { get; }
    public List<MalfVoiceModulatorOption> JobIcons { get; }
    public List<MalfVoiceModulatorCrewMember> Crew { get; }

    public MalfVoiceModulatorState(
        string name,
        string? verb,
        string? speechSounds,
        bool active,
        bool accentHide,
        string? jobIcon,
        List<MalfVoiceModulatorOption> verbs,
        List<MalfVoiceModulatorOption> sounds,
        List<MalfVoiceModulatorOption> jobIcons,
        List<MalfVoiceModulatorCrewMember> crew)
    {
        Name = name;
        Verb = verb;
        SpeechSounds = speechSounds;
        Active = active;
        AccentHide = accentHide;
        JobIcon = jobIcon;
        Verbs = verbs;
        Sounds = sounds;
        JobIcons = jobIcons;
        Crew = crew;
    }
}

[Serializable, NetSerializable]
public sealed record MalfVoiceModulatorCrewMember(NetEntity Entity, string Name, string Job,
    string JobIcon, MalfVoiceCrewHealth Health);

[Serializable, NetSerializable]
public enum MalfVoiceCrewHealth : byte
{
    Unknown,
    Healthy,
    Wounded,
    Critical,
    Dead,
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorCopyCrewEvent(NetEntity target) : EntityEventArgs
{
    public NetEntity Target = target;
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorRefreshCrewEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorOption
{
    public string Id { get; }
    public string Name { get; }

    public MalfVoiceModulatorOption(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorSubmitNameEvent : EntityEventArgs
{
    public readonly string Name;

    public MalfVoiceModulatorSubmitNameEvent(string name)
    {
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorChangeVerbEvent : EntityEventArgs
{
    public readonly string? Verb;

    public MalfVoiceModulatorChangeVerbEvent(string? verb)
    {
        Verb = verb;
    }
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorChangeSoundEvent : EntityEventArgs
{
    public readonly string? SpeechSounds;

    public MalfVoiceModulatorChangeSoundEvent(string? speechSounds)
    {
        SpeechSounds = speechSounds;
    }
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorToggleEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorAccentToggleEvent : EntityEventArgs
{
}

[Serializable, NetSerializable]
public sealed class MalfVoiceModulatorChangeJobIconEvent : EntityEventArgs
{
    public readonly string? JobIcon;

    public MalfVoiceModulatorChangeJobIconEvent(string? jobIcon)
    {
        JobIcon = jobIcon;
    }
}
