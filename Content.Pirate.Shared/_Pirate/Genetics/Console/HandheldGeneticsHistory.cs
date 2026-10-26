// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Genetics.Mutations;
using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Genetics.Console;

public sealed partial class HandheldGeneticsScannerComponent
{
    [DataField(serverOnly: true)]
    public List<HandheldGeneticsScan> Scans = new();

    [DataField(serverOnly: true)]
    public int? SelectedScan;
}

[DataDefinition]
public sealed partial class HandheldGeneticsScan
{
    [DataField]
    public EntityUid Target;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string DnaId = string.Empty;

    [DataField]
    public List<SequenceState> Sequences = new();
}

[Serializable, NetSerializable]
public sealed class HandheldGeneticsHistoryState(List<HandheldGeneticsTarget> targets, int? selectedScan)
{
    /// <summary>Targets are ordered by scan index.</summary>
    public readonly List<HandheldGeneticsTarget> Targets = targets;
    public readonly int? SelectedScan = selectedScan;
}

[Serializable, NetSerializable]
public readonly record struct HandheldGeneticsTarget(string Name, List<int> Numbers);

[Serializable, NetSerializable]
public sealed class HandheldGeneticsSelectScanMessage(int? index) : BoundUserInterfaceMessage
{
    public readonly int? Index = index;
}

public sealed partial class GeneticsConsoleState
{
    public HandheldGeneticsHistoryState? HandheldHistory;
}
