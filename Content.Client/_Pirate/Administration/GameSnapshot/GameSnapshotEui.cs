// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Eui;
using Content.Shared._Pirate.Administration.GameSnapshot;
using Content.Shared.Eui;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;

namespace Content.Client._Pirate.Administration.GameSnapshot;

[UsedImplicitly]
public sealed class GameSnapshotEui : BaseEui
{
    [Dependency] private readonly IClyde _clyde = default!;

    private readonly DefaultWindow _window = new() { SetSize = new Vector2(960, 600), MinSize = new Vector2(350, 220) };
    private readonly ScrollContainer _scroll = new()
    {
        HorizontalExpand = true,
        VerticalExpand = true,
        HScrollEnabled = false,
        VScrollEnabled = false,
    };
    private readonly TextureRect _image = new()
    {
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
        MouseFilter = Control.MouseFilterMode.Pass,
        CanShrink = true,
        HorizontalExpand = true,
        VerticalExpand = true,
    };
    private readonly Label _status = new() { HorizontalExpand = true, ClipText = true };
    private readonly Label _lastFrame = new();
    private readonly Button _zoom = new() { Text = Loc.GetString("admin-spy-zoom"), ToggleMode = true, Disabled = true };
    private readonly Button _refresh = new() { Text = Loc.GetString("admin-spy-refresh"), Disabled = true };
    private OwnedTexture? _texture;
    private GameSnapshotFrame? _incoming;
    private uint _requestId;
    private int _nextOffset;
    private bool _closing;
    private uint _revision;

    public GameSnapshotEui()
    {
        var layout = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var footer = new BoxContainer();
        footer.AddChild(_status);
        footer.AddChild(_lastFrame);
        footer.AddChild(_zoom);
        footer.AddChild(_refresh);
        _scroll.AddChild(_image);
        layout.AddChild(_scroll);
        layout.AddChild(footer);
        _window.Contents.AddChild(layout);
        _refresh.OnPressed += _ => Refresh();
        _image.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick && !_refresh.Disabled)
            {
                Refresh();
                args.Handle();
            }
        };
        _zoom.OnToggled += _ => UpdateZoom();
        _window.OnResized += UpdateZoom;
        _window.OnClose += () =>
        {
            if (!_closing)
                SendMessage(new CloseEuiMessage());
        };
    }

    private void Refresh()
    {
        _refresh.Disabled = true;
        _status.Text = Loc.GetString("admin-spy-waiting");
        SendMessage(new RefreshGameSnapshot());
    }

    private void UpdateZoom()
    {
        var nativeSize = _zoom.Pressed;
        _scroll.HScrollEnabled = _scroll.VScrollEnabled = nativeSize;
        _image.CanShrink = !nativeSize;
        _image.TextureScale = nativeSize ? Vector2.One / _image.UIScale : Vector2.One;
        _image.Stretch = nativeSize ? TextureRect.StretchMode.Keep : TextureRect.StretchMode.KeepAspectCentered;
    }

    public override void Opened() => _window.OpenCentered();

    public override void Closed()
    {
        _closing = true;
        _incoming = null;
        _window.Close();
        _image.Texture = null;
        _texture?.Dispose();
        _texture = null;
    }

    public override async void HandleState(EuiStateBase state)
    {
        if (state is not GameSnapshotState snapshot)
            return;

        var revision = ++_revision;
        _window.Title = Loc.GetString("admin-spy-title", ("player", snapshot.Username));
        var status = snapshot.Status;
        _refresh.Disabled = status is GameSnapshotStatus.Waiting or GameSnapshotStatus.Received or GameSnapshotStatus.Offline;
        if (status != GameSnapshotStatus.Received)
            _incoming = null;

        if (status == GameSnapshotStatus.Received)
        {
            try
            {
                var frame = ReceiveChunk(snapshot);
                if (frame == null)
                    return;

                using var image = await Task.Run(() => GameSnapshotPixels.Decode(frame));
                if (_closing || revision != _revision)
                    return;

                var next = _clyde.LoadTextureFromImage(image, "admin-game-snapshot");
                _image.Texture = next;
                _texture?.Dispose();
                _texture = next;
                _zoom.Disabled = false;
                _lastFrame.Text = Loc.GetString("admin-spy-received", ("time", DateTime.Now.ToString("HH:mm:ss")));
                UpdateZoom();
            }
            catch (Exception e)
            {
                _incoming = null;
                Logger.GetSawmill("admin.spy").Warning($"Could not display the game frame: {e.Message}");
                status = GameSnapshotStatus.Unavailable;
            }
        }

        if (_closing || revision != _revision)
            return;

        _refresh.Disabled = status is GameSnapshotStatus.Waiting or GameSnapshotStatus.Offline;
        _status.Text = status switch
        {
            GameSnapshotStatus.Received => string.Empty,
            GameSnapshotStatus.Waiting => Loc.GetString("admin-spy-waiting"),
            GameSnapshotStatus.Busy => Loc.GetString("admin-spy-busy"),
            GameSnapshotStatus.Offline => Loc.GetString("cmd-spy-offline"),
            GameSnapshotStatus.TimedOut => Loc.GetString("admin-spy-timeout"),
            _ => Loc.GetString("admin-spy-unavailable"),
        };
    }

    private GameSnapshotFrame? ReceiveChunk(GameSnapshotState state)
    {
        if (state.Pixels is not { } pixels
            || !GameSnapshotFrame.ValidChunk(state.Width, state.Height, state.TotalBytes, state.Offset, pixels.Length))
            throw new InvalidDataException();

        if (state.Offset == 0)
        {
            _incoming = new GameSnapshotFrame(state.Width, state.Height, new byte[state.TotalBytes]);
            _requestId = state.RequestId;
            _nextOffset = 0;
        }

        if (_incoming is not { } frame || _requestId != state.RequestId || _nextOffset != state.Offset
            || frame.Width != state.Width || frame.Height != state.Height || frame.Pixels.Length != state.TotalBytes)
            throw new InvalidDataException();

        Array.Copy(pixels, 0, frame.Pixels, state.Offset, pixels.Length);
        _nextOffset += pixels.Length;
        if (_nextOffset < state.TotalBytes)
        {
            _status.Text = Loc.GetString("admin-spy-receiving", ("percent", _nextOffset * 100 / state.TotalBytes));
            SendMessage(new ContinueGameSnapshot(_requestId, _nextOffset));
            return null;
        }

        _incoming = null;
        return frame;
    }
}
