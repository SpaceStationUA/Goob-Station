// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.StationRecords;
using Content.Pirate.Shared.Genetics.Console;
using Content.Shared._Pirate.StationRecords;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsConsoleWindow
{
    public void AttachInteractionSounds(BoundUserInterface bui)
    {
        RecordConsoleSounds.Attach(bui, this);
        Tabs.OnTabChanged += _ => RecordConsoleSounds.Play(Tabs, RecordConsoleSound.Button);
        Sequencer.OnSetBase += message =>
        {
            // Shared hooks cover left clicks; include right and Ctrl clicks.
            if (message.Cycle != GeneticsCycle.Next)
                RecordConsoleSounds.Play(Sequencer, RecordConsoleSound.Button);
        };
    }
}
