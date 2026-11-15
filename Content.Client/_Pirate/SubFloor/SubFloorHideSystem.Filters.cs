// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Power.Visualizers;
using Content.Shared._Pirate.SubFloor;
using Content.Shared.Atmos.Components;

namespace Content.Client.SubFloor;

public sealed partial class SubFloorHideSystem
{
    private bool _showWires;
    private bool _showPipes;

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowWires
    {
        get => _showWires;
        set
        {
            if (_showWires == value)
                return;

            _showWires = value;
            SendShowRequest();
        }
    }

    [ViewVariables(VVAccess.ReadWrite)]
    public bool ShowPipes
    {
        get => _showPipes;
        set
        {
            if (_showPipes == value)
                return;

            _showPipes = value;
            SendShowRequest();
        }
    }

    // The server must send all sub-floor entities; category filtering runs on the client.
    private void SendShowRequest()
    {
        RaiseNetworkEvent(new ShowSubfloorRequestEvent
        {
            Value = _showAll || _showWires || _showPipes,
        });
    }

    private void ResetFilters()
    {
        ShowWires = false;
        ShowPipes = false;
    }

    private bool IsFilterRevealed(EntityUid uid)
    {
        if (!_showWires && !_showPipes)
            return false;

        return GetCategory(uid) switch
        {
            SubFloorCategory.Wires => _showWires,
            SubFloorCategory.Pipes => _showPipes,
            _ => false,
        };
    }

    private SubFloorCategory? GetCategory(EntityUid uid)
    {
        if (TryComp<SubFloorCategoryComponent>(uid, out var category))
            return category.Category;

        if (HasComp<CableVisualizerComponent>(uid))
            return SubFloorCategory.Wires;

        if (HasComp<PipeAppearanceComponent>(uid) || HasComp<AtmosPipeLayersComponent>(uid))
            return SubFloorCategory.Pipes;

        return null;
    }
}
