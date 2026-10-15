// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.StationRecords;

[Serializable, NetSerializable]
public enum RecordConsoleSound : byte
{
    Button,

    DropdownOpen,

    DropdownSelect,

    Submit,

    SwitchRecord,
}

[Serializable, NetSerializable]
public sealed class RecordConsoleSoundMessage(RecordConsoleSound sound) : BoundUserInterfaceMessage
{
    public readonly RecordConsoleSound Sound = sound;
}

[RegisterComponent]
public sealed partial class RecordConsoleSoundsComponent : Component
{
    [DataField]
    public SoundSpecifier? Button = new SoundCollectionSpecifier("RecordsConsoleButton", AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier? DropdownOpen = new SoundCollectionSpecifier("RecordsConsoleButton", AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier? DropdownSelect = new SoundCollectionSpecifier("RecordsConsoleButton", AudioParams.Default.WithVolume(-2f));

    [DataField]
    public SoundSpecifier? Submit = new SoundCollectionSpecifier("RecordsConsoleKeyboardClicks",
        AudioParams.Default.WithVariation(0.05f));

    [DataField]
    public SoundSpecifier? SwitchRecord = new SoundCollectionSpecifier("RecordsConsoleButton", AudioParams.Default.WithVolume(-2f));

    // Drop rapid clicks so they do not stack.
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.05);

    public TimeSpan NextSound;

    public SoundSpecifier? Get(RecordConsoleSound sound) => sound switch
    {
        RecordConsoleSound.Button => Button,
        RecordConsoleSound.DropdownOpen => DropdownOpen,
        RecordConsoleSound.DropdownSelect => DropdownSelect,
        RecordConsoleSound.Submit => Submit,
        RecordConsoleSound.SwitchRecord => SwitchRecord,
        _ => null,
    };
}
