// SPDX-FileCopyrightText: 2025 Coenx-flex
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.UserInterface.Controls;
using Content.Shared._Pirate.CorticalBorer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._Pirate.CorticalBorer;

[UsedImplicitly]
public sealed class CorticalBorerDispenserBoundUserInterface : BoundUserInterface
{
    private CorticalBorerDispenserWindow? _window;

    public CorticalBorerDispenserBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CorticalBorerDispenserWindow>();
        _window.SetInfoFromEntity(EntMan, Owner);
        _window.AmountGrid.OnButtonPressed += amount =>
            SendMessage(new CorticalBorerDispenserSetInjectAmountMessage(int.Parse(amount)));
        _window.OnDispenseReagentButtonPressed += id =>
            SendMessage(new CorticalBorerDispenserInjectMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        _window?.UpdateState((CorticalBorerDispenserBoundUserInterfaceState) state);
    }
}
