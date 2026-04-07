// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Map;

namespace Content.Server._Pirate.Photo;
public sealed class PhotoCameraTakeImageEvent : EntityEventArgs
{
    public EntityUid Camera { get; }
    public EntityUid User { get; }
    public MapCoordinates PhotoPosition { get; }
    public float Zoom { get; }

    public PhotoCameraTakeImageEvent(EntityUid camera, EntityUid user, MapCoordinates photoPosition, float zoom)
    {
        Camera = camera;
        User = user;
        PhotoPosition = photoPosition;
        Zoom = zoom;
    }
}


