// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Shared._Pirate.Administration.GameSnapshot;
using Robust.Client.Graphics;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.Client._Pirate.Administration.GameSnapshot;

public sealed class GameSnapshotCapture
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private Capture? _active;
    private TimeSpan _nextCapture;

    public void Initialize()
    {
        _net.RegisterNetMessage<MsgRequestGameSnapshot>(OnRequest, NetMessageAccept.Client);
        _net.RegisterNetMessage<MsgGameSnapshotReply>(accept: NetMessageAccept.Server);
        _net.Disconnect += (_, _) => _active = null;
    }

    private void OnRequest(MsgRequestGameSnapshot request)
    {
        if (request.Offset != 0)
        {
            if (_active is { Frame: not null } current && current.Id == request.RequestId
                && current.Channel == request.MsgChannel && current.NextOffset == request.Offset
                && _timing.RealTime < current.Deadline)
                SendChunk(current);
            return;
        }

        var now = _timing.RealTime;
        if (_clyde.MainWindow.IsMinimized || now < _nextCapture)
        {
            _net.ClientSendMessage(new MsgGameSnapshotReply { RequestId = request.RequestId });
            return;
        }

        _nextCapture = now + MsgRequestGameSnapshot.Cooldown;
        var capture = new Capture(request.RequestId, request.MsgChannel, now + MsgRequestGameSnapshot.Timeout);
        _active = capture;
        Timer.Spawn(MsgRequestGameSnapshot.Timeout, () =>
        {
            if (_active is { } active && _timing.RealTime >= active.Deadline)
                _active = null;
        });

        // The same final game framebuffer as F2, after the main window's UI is drawn.
        _clyde.Screenshot(ScreenshotType.Final, image => Encode(capture, image));
    }

    private async void Encode(Capture capture, Image<Rgb24> image)
    {
        using (image)
        {
            if (_active != capture || _timing.RealTime >= capture.Deadline)
                return;

            try
            {
                capture.Frame = await Task.Run(() => GameSnapshotPixels.Encode(image));
            }
            catch (Exception e)
            {
                Logger.GetSawmill("admin.spy").Warning($"Could not encode the game frame: {e.Message}");
            }
        }

        if (_active == capture && _net.ServerChannel == capture.Channel && _timing.RealTime < capture.Deadline)
            SendChunk(capture);
    }

    private void SendChunk(Capture capture)
    {
        var reply = new MsgGameSnapshotReply { RequestId = capture.Id, Offset = capture.NextOffset };
        if (capture.Frame is { } frame)
        {
            reply.Width = frame.Width;
            reply.Height = frame.Height;
            reply.TotalBytes = frame.Pixels.Length;
            var count = Math.Min(GameSnapshotFrame.ChunkBytes, reply.TotalBytes - reply.Offset);
            reply.Pixels = new ReadOnlyMemory<byte>(frame.Pixels, reply.Offset, count);
            capture.NextOffset += count;
        }

        if (capture.NextOffset == reply.TotalBytes)
            _active = null;

        _net.ClientSendMessage(reply);
    }

    private sealed class Capture(uint id, INetChannel channel, TimeSpan deadline)
    {
        public readonly uint Id = id;
        public readonly INetChannel Channel = channel;
        public readonly TimeSpan Deadline = deadline;
        public GameSnapshotFrame? Frame;
        public int NextOffset;
    }
}
