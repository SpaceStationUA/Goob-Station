// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NodeContainer.Nodes;
using Content.Shared._Pirate.Atmos.Piping;
using Content.Shared.Atmos;

namespace Content.Server._Pirate.Atmos.Piping.Nodes;

[DataDefinition]
[Virtual]
public partial class HeatExchangePipeNode : PipeNode
{
    public override AtmosPipePortKind PortKindInDirection(PipeDirection direction)
        => AtmosPipePortKind.HeatExchange;
}
