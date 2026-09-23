// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Client.SurveillanceCamera.UI;

public sealed partial class SurveillanceCameraMonitorWindow
{
    private readonly Dictionary<NetEntity, (NetCoordinates Coordinates, bool Selected, bool Mobile)> _pendingBlips = new();

    private void QueuePendingBlip(NetEntity entity, NetCoordinates coordinates, bool selected, bool mobile)
        => _pendingBlips[entity] = (coordinates, selected, mobile);

    private void ClearPendingBlips()
        => _pendingBlips.Clear();

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        foreach (var (entity, data) in _pendingBlips.ToArray())
        {
            if (!_entManager.TryGetEntity(data.Coordinates.NetEntity, out var resolved) ||
                !_entManager.HasComponent<TransformComponent>(resolved.Value))
                continue;

            _pendingBlips.Remove(entity);
            AddTrackedEntityToNavMap(entity, data.Coordinates, data.Selected, data.Mobile);
        }
    }
}
