// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Client._Pirate.Photo.UI;
using Content.Shared._Pirate.Photo;

namespace Content.Client._Pirate.Photo;

public sealed partial class PhotoSystem : SharedPhotoSystem
{
    private readonly Dictionary<PhotoCameraComponent, PhotoCameraBoundUserInterface> _activeCameras = new();
    public IReadOnlyDictionary<PhotoCameraComponent, PhotoCameraBoundUserInterface> ActiveCameras => _activeCameras;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var activeCamerasSnapshot = new List<KeyValuePair<PhotoCameraComponent, PhotoCameraBoundUserInterface>>(_activeCameras);
        foreach (var (component, window) in activeCamerasSnapshot)
        {
            window.UpdateControl(component, frameTime);
        }
    }

    public void OpenCameraUi(PhotoCameraComponent component, PhotoCameraBoundUserInterface window)
    {
        if (_activeCameras.ContainsKey(component))
            return;

        _activeCameras.Add(component, window);
    }

    public void CloseCameraUi(PhotoCameraComponent component)
    {
        if (!_activeCameras.ContainsKey(component))
            return;

        _activeCameras.Remove(component);
    }
}


