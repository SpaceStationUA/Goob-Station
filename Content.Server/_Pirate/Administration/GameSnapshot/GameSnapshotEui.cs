// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.EUI;
using Content.Shared._Pirate.Administration.GameSnapshot;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Administration.GameSnapshot;

public sealed class GameSnapshotEui(GameSnapshotManager snapshots, ICommonSession target) : BaseEui
{
    internal ICommonSession Target => target;
    private GameSnapshotState? _state;

    internal void Show(GameSnapshotStatus status, MsgGameSnapshotReply? frame = null)
    {
        _state = new GameSnapshotState(target.Name, status);
        if (frame != null)
        {
            _state.RequestId = frame.RequestId;
            _state.Width = frame.Width;
            _state.Height = frame.Height;
            _state.TotalBytes = frame.TotalBytes;
            _state.Offset = frame.Offset;
            _state.Pixels = frame.Pixels.ToArray();
        }
        StateDirty();
    }

    public override EuiStateBase GetNewState()
    {
        var state = _state!;
        _state = null;
        return state;
    }

    public override void HandleMessage(EuiMessageBase message)
    {
        base.HandleMessage(message);
        if (message is RefreshGameSnapshot)
            snapshots.Request(this);
        else if (message is ContinueGameSnapshot next)
            snapshots.Continue(this, next);
    }

    public override void Closed() => snapshots.Closed(this);
}
