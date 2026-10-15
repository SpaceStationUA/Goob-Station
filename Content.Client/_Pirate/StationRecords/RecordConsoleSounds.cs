// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.StationRecords;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.StationRecords;

public static class RecordConsoleSounds
{
    private static readonly AttachedProperty<BoundUserInterface?> ConsoleProperty =
        AttachedProperty<BoundUserInterface?>.CreateNull("RecordConsole", typeof(RecordConsoleSounds));

    private static readonly AttachedProperty<bool> HookedProperty =
        AttachedProperty<bool>.Create("RecordConsoleSoundHooked", typeof(RecordConsoleSounds));

    private static readonly AttachedProperty<bool> MutedProperty =
        AttachedProperty<bool>.Create("RecordConsoleSoundMuted", typeof(RecordConsoleSounds));

    public static void Attach(BoundUserInterface bui, Control root)
    {
        root.SetValue(ConsoleProperty, bui);
        Hook(bui, root);
    }

    public static void AttachDialog(Control owner, Control dialog)
    {
        if (FindConsole(owner) is { } bui)
            Attach(bui, dialog);
    }

    public static void Mute(Control control)
    {
        control.SetValue(MutedProperty, true);
    }

    public static void Play(Control source, RecordConsoleSound sound)
    {
        if (FindConsole(source) is { } bui)
            Play(bui, sound);
    }

    private static void Play(BoundUserInterface bui, RecordConsoleSound sound)
    {
        IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<RecordConsoleSoundSystem>().Play(bui, sound);
    }

    private static BoundUserInterface? FindConsole(Control control)
    {
        for (Control? at = control; at != null; at = at.Parent)
        {
            if (at.GetValue(ConsoleProperty) is { } bui)
                return bui;
        }

        return null;
    }

    private static void Hook(BoundUserInterface bui, Control control)
    {
        if (control.GetValue(HookedProperty))
            return;

        control.SetValue(HookedProperty, true);
        switch (control)
        {
            case OptionButton dropdown:
                dropdown.OnPressed += _ => Play(bui, RecordConsoleSound.DropdownOpen);
                dropdown.OnItemSelected += _ => Play(bui, RecordConsoleSound.DropdownSelect);
                break;
            // Closing the console has no click sound.
            case BaseButton button when button.Name != "CloseButton":
                button.OnPressed += _ =>
                {
                    if (!button.GetValue(MutedProperty))
                        Play(bui, RecordConsoleSound.Button);
                };
                break;
            case LineEdit field:
                field.OnTextEntered += _ => Play(bui, RecordConsoleSound.Submit);
                break;
        }

        control.OnChildAdded += child => Hook(bui, child);
        foreach (var child in control.Children)
        {
            Hook(bui, child);
        }
    }
}

public sealed class RecordConsoleSoundSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public void Play(BoundUserInterface bui, RecordConsoleSound sound)
    {
        if (!TryComp<RecordConsoleSoundsComponent>(bui.Owner, out var comp)
            || _timing.RealTime < comp.NextSound
            || comp.Get(sound) is not { } specifier)
        {
            return;
        }

        comp.NextSound = _timing.RealTime + comp.Cooldown;
        _audio.PlayEntity(specifier, Filter.Local(), bui.Owner, false);
        bui.SendMessage(new RecordConsoleSoundMessage(sound));
    }
}
