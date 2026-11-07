// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Console;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsConsoleWindow
{
    private void UpdateEditing(GeneticsScannerComponent scanner)
    {
        var enabled = _hasScanner == true && _mob is {} mob && _genome.IsScanned(mob) &&
            _entMan.System<GeneticsConsoleSystem>().CanWorkOn((_uid, scanner), mob);
        Sequencer.SetEditingEnabled(enabled);
    }
}

public sealed partial class GeneticSequencer
{
    public void SetEditingEnabled(bool enabled)
    {
        Puzzle.SetEditingEnabled(enabled);
    }
}

public sealed partial class GenePuzzle
{
    private bool _editingEnabled = true;

    public void SetEditingEnabled(bool enabled)
    {
        if (_editingEnabled == enabled)
            return;

        _editingEnabled = enabled;
        UpdateBaseButtons();
        UpdateSequenceButton();
    }
}
