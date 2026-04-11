// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Photo;
using Robust.Client.Audio;
using Robust.Client.GameObjects;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Sources;
using System.Numerics;

namespace Content.Client._Pirate.Photo.UI;

public sealed class PhotoCameraBoundUserInterface : BoundUserInterface
{
    private const float ControlAudioActiveVolume = 2f;
    private const float ControlAudioIdleVolume = -20f;
    // Keep 15% of ViewBox pan range even at maximum zoom so framing does not snap back to center.
    private const float MaxZoomPanRatio = 0.15f;

    private readonly EyeSystem _eyeSystem;
    private readonly PhotoSystem _photoSystem;
    private readonly TransformSystem _transform;
    private readonly PhotoCaptureEntityDetectorSystem _photoEntityDetector;

    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IAudioManager _audioManager = default!;

    [ViewVariables]
    private PhotoCameraWindow? _window;

    [ViewVariables]
    private EntityUid? _cameraEntity;

    private Vector2 _zoomPos = Vector2.Zero;
    private float _zoomValue = 1f;

    private float _controlVolume;
    private IAudioSource? _controlSound;

    public PhotoCameraBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeSystem = EntMan.System<EyeSystem>();
        _photoSystem = EntMan.System<PhotoSystem>();
        _transform = EntMan.System<TransformSystem>();
        _photoEntityDetector = EntMan.System<PhotoCaptureEntityDetectorSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PhotoCameraWindow>();

        _window.OnTakeImageAttempt += AttemptTakeImage;

        if (!_cache.TryGetResource("/Audio/_Pirate/Effects/servo_effect.ogg", out AudioResource? resource))
            return;

        var source = _audioManager.CreateAudioSource(resource);
        if (source == null)
            return;

        source.Global = true;
        source.Looping = true;
        // Start muted; volume fades in when camera moves (see UpdateControl)
        source.Volume = float.NegativeInfinity;
        source.Restart();

        _controlSound = source;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not PhotoCameraUiState cast)
            return;

        _cameraEntity = EntMan.GetEntity(cast.CameraEntity);

        if (EntMan.TryGetComponent<PhotoCameraComponent>(_cameraEntity, out var component))
        {
            _photoSystem.OpenCameraUi(component, this);
            UpdateControl(component, 1);
        }

        if (EntMan.TryGetComponent<EyeComponent>(_cameraEntity, out var eye))
        {
            _window.UpdateState(eye.Eye, cast.HasPaper);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_window != null)
            _window.OnTakeImageAttempt -= AttemptTakeImage;

        if (_cameraEntity != null)
        {
            if (EntMan.TryGetComponent<PhotoCameraComponent>(_cameraEntity, out var component))
                _photoSystem.CloseCameraUi(component);

            _cameraEntity = null;
        }

        _controlSound?.Dispose();
        _window?.OnDispose();
    }

    public void UpdateControl(PhotoCameraComponent component, float frameTime)
    {
        if (!TryGetControlContext(out var cameraUid, out var window, out var worldAngle, out var localAngle))
            return;

        var (nextPos, nextZoom, delta) = ComputeNextControlState(component, window, frameTime);
        _zoomPos = nextPos;
        _zoomValue = nextZoom;
        window.ZoomInput = 0;

        var rotateAngle = worldAngle.Opposite() - (localAngle - localAngle.RoundToCardinalAngle());
        _eyeSystem.SetOffset(cameraUid, rotateAngle.RotateVec(nextPos));
        _eyeSystem.SetZoom(cameraUid, new Vector2(nextZoom));
        _eyeSystem.SetRotation(cameraUid, -rotateAngle);

        UpdateControlAudio(delta, frameTime);
    }

    private bool TryGetControlContext(out EntityUid cameraUid, out PhotoCameraWindow window, out Angle worldAngle, out Angle localAngle)
    {
        cameraUid = EntityUid.Invalid;
        window = null!;
        worldAngle = Angle.Zero;
        localAngle = Angle.Zero;

        if (_cameraEntity is not { } uid || _window == null)
            return false;

        if (!EntMan.HasComponent<TransformComponent>(uid))
            return false;

        cameraUid = uid;
        window = _window;
        worldAngle = _transform.GetWorldRotation(uid);
        var grid = _transform.GetGrid(uid);
        if (grid != null)
            localAngle = worldAngle - _transform.GetWorldRotation(grid.Value);

        return true;
    }

    private (Vector2 Pos, float Zoom, System.Numerics.Vector3 Delta) ComputeNextControlState(
        PhotoCameraComponent component,
        PhotoCameraWindow window,
        float frameTime)
    {
        var pos = _zoomPos + window.MoveInput * _zoomValue * frameTime;

        var zoomRange = component.MaxZoom - component.MinZoom;
        var zoom = Math.Clamp(_zoomValue + window.ZoomInput * frameTime * zoomRange, component.MinZoom, component.MaxZoom);
        var zoomRatio = Math.Abs(zoomRange) > float.Epsilon
            ? (zoom - component.MinZoom) / zoomRange
            : 0f;

        // 0.4f softens the falloff curve: higher zoom still reduces pan range, but less aggressively than linear.
        // These values were chosen from gameplay tuning so max zoom keeps limited framing freedom without exposing the full ViewBox.
        var panRatio = MaxZoomPanRatio + (1 - MaxZoomPanRatio) * MathF.Pow(1 - zoomRatio, 0.4f);
        var xClamp = component.ViewBox.X * 0.5f * panRatio;
        var yClamp = component.ViewBox.Y * 0.5f * panRatio;
        pos.X = Math.Clamp(pos.X, -xClamp, xClamp);
        pos.Y = Math.Clamp(pos.Y, -yClamp, yClamp);

        var delta = new System.Numerics.Vector3(_zoomPos - pos, _zoomValue - zoom);
        return (pos, zoom, delta);
    }

    private void UpdateControlAudio(System.Numerics.Vector3 delta, float frameTime)
    {
        if (_controlSound == null)
            return;

        var targetVolume = delta != System.Numerics.Vector3.Zero ? ControlAudioActiveVolume : ControlAudioIdleVolume;
        _controlVolume = delta.Z != 0 ? ControlAudioActiveVolume : _controlVolume;
        _controlVolume = Math.Clamp(_controlVolume + (targetVolume - _controlVolume) * frameTime, ControlAudioIdleVolume, ControlAudioActiveVolume);

        _controlSound.Volume = _controlVolume > ControlAudioIdleVolume ? _controlVolume : float.NegativeInfinity;
    }

    private void AttemptTakeImage()
    {
        if (_window == null)
            return;

        var window = _window;

        window.RenderImage((bytes, previewBytes) =>
        {
            if (_cameraEntity == null)
                return;

            var capturedEntities = _photoEntityDetector.CaptureVisibleEntities(window.CameraViewport);

            var message = new PhotoCameraTakeImageMessage(bytes, previewBytes, _zoomValue, capturedEntities);
            SendMessage(message);
        });
    }
}


