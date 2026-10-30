// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.Genetics.Console;

/// <summary>
/// Console component for printing scanned mutation ids or sequences of a selected mutation to a sheet of paper.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(GeneticsConsoleSystem))]
[AutoGenerateComponentPause, AutoGenerateComponentState]
public sealed partial class GeneticsPrintoutComponent : Component
{
    [DataField]
    public EntProtoId Paper = "PaperGeneticsScanner";

    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(5);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField, AutoNetworkedField]
    public TimeSpan NextPrint;
}

[Serializable, NetSerializable]
public sealed class GeneticsPrintScanMessage(uint? index = null) : BoundUserInterfaceMessage
{
    public readonly uint? Index = index;
}
