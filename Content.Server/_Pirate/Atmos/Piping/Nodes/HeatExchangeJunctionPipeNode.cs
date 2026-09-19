// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.NodeContainer.Nodes;
using Content.Shared._Pirate.Atmos.Piping;
using Content.Shared.Atmos;

namespace Content.Server._Pirate.Atmos.Piping.Nodes;

[DataDefinition]
[Virtual]
public partial class HeatExchangeJunctionPipeNode : PipeNode
{
    [DataField("heatExchangeDirection")]
    public PipeDirection OriginalHeatExchangeDirection = PipeDirection.South;

    [ViewVariables]
    public PipeDirection CurrentHeatExchangeDirection { get; private set; }

    public override void Initialize(EntityUid owner, IEntityManager entMan)
    {
        CurrentHeatExchangeDirection = OriginalHeatExchangeDirection;

        base.Initialize(owner, entMan);
    }

    protected override bool OnPipeDirectionUpdated(Angle rotation)
    {
        var old = CurrentHeatExchangeDirection;
        CurrentHeatExchangeDirection = OriginalHeatExchangeDirection.RotatePipeDirection(rotation);

        return old != CurrentHeatExchangeDirection;
    }

    public override AtmosPipePortKind PortKindInDirection(PipeDirection direction)
        => CurrentHeatExchangeDirection.HasDirection(direction)
            ? AtmosPipePortKind.HeatExchange
            : AtmosPipePortKind.Standard;
}
