// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Index;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.Index;

[UsedImplicitly]
public sealed class IndexPagerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private IndexPagerWindow? _window;

    public IndexPagerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<IndexPagerWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not IndexPagerBoundUserInterfaceState pagerState)
            return;

        _window?.UpdateState(pagerState);
    }
}
