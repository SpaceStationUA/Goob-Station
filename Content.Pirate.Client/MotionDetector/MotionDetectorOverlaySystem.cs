// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Hands.Systems;
using Content.Client.UserInterface.Systems.Viewport;
using Content.Pirate.Shared.MotionDetector;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Pirate.Client.MotionDetector;

public sealed class MotionDetectorOverlaySystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly IClientNetManager _net = default!;
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (!_overlay.HasOverlay<MotionDetectorOverlay>())
            _overlay.AddOverlay(new MotionDetectorOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<MotionDetectorOverlay>();
    }

    public void DrawBlips(
        DrawingHandleWorld handle,
        ref EntityUid? activeDetector,
        ref TimeSpan lastScan,
        List<Vector2> blips,
        Texture texture)
    {
        if (_player.LocalEntity is not { } player)
            return;

        var playerCoordinates = _transform.GetMapCoordinates(player);
        var time = _timing.CurTime;

        foreach (var held in _hands.EnumerateHeld(player))
        {
            if (!TryComp<MotionDetectorComponent>(held, out var detector) || !detector.Enabled)
                continue;

            var duration = detector.ScanDuration;
            if (_net.ServerChannel is { } channel)
                duration += TimeSpan.FromMilliseconds(channel.Ping / 2f);

            if (time > detector.LastScan + duration)
                continue;

            if (activeDetector != held || lastScan != detector.LastScan)
            {
                activeDetector = held;
                lastScan = detector.LastScan;
                blips.Clear();

                foreach (var blip in detector.Blips)
                {
                    if (blip.Coordinates.MapId != playerCoordinates.MapId)
                        continue;

                    var difference = blip.Coordinates.Position - new Vector2(0.5f, 0.5f) - playerCoordinates.Position;
                    blips.Add(difference);
                }
            }

            float viewportHeight = ViewportUIController.ViewportHeight;
            float viewportWidth = _config.GetCVar(CCVars.ViewportWidth);
            var eye = _eye.CurrentEye;
            if (eye.Rotation.GetCardinalDir() is Direction.East or Direction.West)
                (viewportWidth, viewportHeight) = (viewportHeight, viewportWidth);

            viewportWidth *= eye.Zoom.X;
            viewportHeight *= eye.Zoom.Y;

            foreach (var blip in blips)
            {
                var capped = blip;
                Cap(ref capped.X, viewportWidth);
                Cap(ref capped.Y, viewportHeight);
                handle.DrawTexture(texture, playerCoordinates.Position + capped);
            }

            return;
        }

        activeDetector = null;
        blips.Clear();
    }

    private static void Cap(ref float value, float size)
    {
        var max = Math.Max(size / 2f - 0.5f, 0f);
        value = Math.Clamp(value, -max, max);
    }
}
