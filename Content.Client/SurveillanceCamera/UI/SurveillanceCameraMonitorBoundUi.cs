// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Eye;
using Content.Shared._Pirate.ZLevels.Monitoring; // Pirate: multiz
using Content.Shared.SurveillanceCamera;
using Robust.Client.UserInterface;

namespace Content.Client.SurveillanceCamera.UI;

public sealed class SurveillanceCameraMonitorBoundUserInterface : BoundUserInterface
{
    private readonly EyeLerpingSystem _eyeLerpingSystem;
    private readonly SurveillanceCameraMonitorSystem _surveillanceCameraMonitorSystem;

    [ViewVariables]
    private SurveillanceCameraMonitorWindow? _window;

    [ViewVariables]
    private EntityUid? _currentCamera;

    public SurveillanceCameraMonitorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _eyeLerpingSystem = EntMan.System<EyeLerpingSystem>();
        _surveillanceCameraMonitorSystem = EntMan.System<SurveillanceCameraMonitorSystem>();
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SurveillanceCameraMonitorWindow>();

        _window.CameraSelected += OnCameraSelected;
        _window.CameraRefresh += OnCameraRefresh;
        _window.SubnetRefresh += OnSubnetRefresh;
        _window.CameraSwitchTimer += OnCameraSwitchTimer;
        _window.CameraDisconnect += OnCameraDisconnect;
        _window.ZLevelSelected += OnZLevelSelected; // Pirate: multiz

        _window.SetEntity(Owner); // Goobstation
    }

    #region Pirate: multiz
    private void OnZLevelSelected(NetEntity? grid, int depth)
    {
        SendMessage(new CEZMonitoringConsoleLevelSelectedMessage(grid, depth));
    }
    #endregion

    private void OnCameraSelected(string address)
    {
        SendMessage(new SurveillanceCameraMonitorSwitchMessage(address));
    }

    private void OnCameraSwitchTimer()
    {
        _surveillanceCameraMonitorSystem.AddTimer(Owner, _window!.OnSwitchTimerComplete);
    }

    private void OnCameraRefresh()
    {
        SendMessage(new SurveillanceCameraRefreshCamerasMessage());
    }

    private void OnSubnetRefresh()
    {
        SendMessage(new SurveillanceCameraRefreshSubnetsMessage());
    }

    private void OnCameraDisconnect()
    {
        SendMessage(new SurveillanceCameraDisconnectMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not SurveillanceCameraMonitorUiState cast)
        {
            return;
        }

        var active = EntMan.GetEntity(cast.ActiveCamera);

        EntMan.TryGetComponent<TransformComponent>(Owner, out var xform); // Goobstation
        var monitor = Owner; // Goobstation
        var monitorCoords = xform?.Coordinates; // Goobstation

        if (active == null)
        {
            _window.UpdateState(null, null, cast.ActiveAddress, cast.Cameras, cast.MobileCameras, monitor, monitorCoords); // Pirate: multiz

            if (_currentCamera != null)
            {
                _surveillanceCameraMonitorSystem.RemoveTimer(Owner);
                _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
                _currentCamera = null;
            }
        }
        else
        {
            if (_currentCamera == null)
            {
                _eyeLerpingSystem.AddEye(active.Value);
                _currentCamera = active;
            }
            else if (_currentCamera != active)
            {
                _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
                _eyeLerpingSystem.AddEye(active.Value);
                _currentCamera = active;
            }

            if (EntMan.TryGetComponent<EyeComponent>(active, out var eye))
            {
                _window.UpdateState(eye.Eye, active, cast.ActiveAddress, cast.Cameras, cast.MobileCameras, monitor, monitorCoords); // Pirate: multiz
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (_currentCamera != null)
        {
            _eyeLerpingSystem.RemoveEye(_currentCamera.Value);
            _currentCamera = null;
        }

        if (disposing)
        {
            _window?.Dispose();
        }
    }
}
