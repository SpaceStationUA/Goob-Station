// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Item;
using Content.Shared.Tools;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Storage.LinenBin;
[RegisterComponent, NetworkedComponent]
[Access(typeof(LinenBinSystem))]
public sealed partial class LinenBinComponent : Component
{
    [DataField]
    public string HiddenContainerId = "linen-bin-hidden";

    [ViewVariables]
    public ContainerSlot HiddenSlot = default!;
    [DataField]
    public ProtoId<ItemSizePrototype> MaxHiddenSize = "Normal";
    [DataField]
    public ProtoId<ToolQualityPrototype> DeconstructQuality = "Screwing";
    [DataField]
    public ProtoId<ToolQualityPrototype> AnchorQuality = "Anchoring";
}
