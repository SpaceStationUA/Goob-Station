// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Resources;
using Content.Pirate.Shared.AtmosLinks;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.Enums;
using Robust.Shared.Map;

namespace Content.Pirate.Client.AtmosLinks;

public sealed class AtmosLinkOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly IResourceCache _cache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;

    private readonly AtmosLinkOverlaySystem _system;
    private readonly SharedTransformSystem _transform;
    private readonly Font _font;

    private const float OrphanBoxSize = 0.8f;
    private const float SourceMarkerRadius = 0.15f;

    private static readonly Color OrphanColor = Color.Red;

    public override OverlaySpace Space => OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace;

    public AtmosLinkOverlay(AtmosLinkOverlaySystem system)
    {
        IoCManager.InjectDependencies(this);

        _system = system;
        _transform = _entManager.System<SharedTransformSystem>();
        _font = _cache.GetFont("/Fonts/NotoSans/NotoSans-Regular.ttf", 10);

        ZIndex = 200;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (args.Space == OverlaySpace.ScreenSpace)
        {
            args.ScreenHandle.SetTransform(Matrix3x2.Identity);
            DrawLabels(args);
            return;
        }

        var handle = args.WorldHandle;

        // The placement ghost (ZIndex 100) draws its sprite through SpriteSystem, which leaves the
        // cursor-positioned transform on the world handle. We draw after it (ZIndex 200), so without this
        // reset every line and marker slides around with the mouse while something is being placed.
        handle.SetTransform(Matrix3x2.Identity);

        foreach (var group in _system.Groups)
        {
            if (!TryGetWorldPos(group.Source, args.MapId, out var source))
                continue;

            var color = GetLinkColor(group.Source);

            foreach (var target in group.Targets)
            {
                if (!TryGetWorldPos(target, args.MapId, out var targetPos))
                    continue;

                handle.DrawLine(source, targetPos, color);
            }

            handle.DrawCircle(source, SourceMarkerRadius, color);
        }

        foreach (var orphan in _system.Orphans)
        {
            if (!TryGetWorldPos(orphan.Position, args.MapId, out var pos))
                continue;

            if (!args.WorldAABB.Contains(pos))
                continue;

            var box = Box2.CenteredAround(pos, new Vector2(OrphanBoxSize, OrphanBoxSize));
            handle.DrawRect(box, OrphanColor.WithAlpha(0.25f));
            handle.DrawRect(box, OrphanColor, false);

            handle.DrawLine(box.BottomLeft, box.TopRight, OrphanColor);
            handle.DrawLine(box.TopLeft, box.BottomRight, OrphanColor);
        }
    }

    private void DrawLabels(in OverlayDrawArgs args)
    {
        var uiScale = _ui.RootControl.UIScale;
        var offset = new Vector2(-10f, -14f) * uiScale;

        foreach (var orphan in _system.Orphans)
        {
            if (!TryGetWorldPos(orphan.Position, args.MapId, out var pos))
                continue;

            if (!args.WorldAABB.Contains(pos))
                continue;

            var screen = _eye.WorldToScreen(pos).Rounded();
            args.ScreenHandle.DrawString(_font, screen + offset, GetLabel(orphan.Kind), uiScale, GetKindColor(orphan.Kind));
        }
    }

    private bool TryGetWorldPos(NetCoordinates netCoords, MapId mapId, out Vector2 world)
    {
        world = default;

        var coords = _entManager.GetCoordinates(netCoords);
        if (!coords.IsValid(_entManager))
            return false;

        var mapCoords = _transform.ToMapCoordinates(coords);
        if (mapCoords.MapId != mapId)
            return false;

        world = mapCoords.Position;
        return true;
    }

    /// <summary>
    ///     Stable per-device-list color so the same alarm keeps its color across snapshot refreshes.
    /// </summary>
    private static Color GetLinkColor(NetCoordinates source)
    {
        var hue = (uint) source.GetHashCode() % 360 / 360f;
        return Color.FromHsv(new Vector4(hue, 0.7f, 1f, 0.8f));
    }

    private static Color GetKindColor(AtmosLinkDeviceKind kind)
    {
        return kind switch
        {
            AtmosLinkDeviceKind.AirAlarm => Color.Orange,
            AtmosLinkDeviceKind.FireAlarm => Color.OrangeRed,
            AtmosLinkDeviceKind.Sensor => Color.Aquamarine,
            AtmosLinkDeviceKind.Vent => Color.SkyBlue,
            AtmosLinkDeviceKind.Scrubber => Color.LightGreen,
            AtmosLinkDeviceKind.Firelock => Color.Yellow,
            _ => Color.White,
        };
    }

    private static string GetLabel(AtmosLinkDeviceKind kind)
    {
        return kind switch
        {
            AtmosLinkDeviceKind.AirAlarm => "ALARM",
            AtmosLinkDeviceKind.FireAlarm => "FIRE",
            AtmosLinkDeviceKind.Sensor => "SENSOR",
            AtmosLinkDeviceKind.Vent => "VENT",
            AtmosLinkDeviceKind.Scrubber => "SCRUB",
            AtmosLinkDeviceKind.Firelock => "FLOCK",
            _ => "ATMOS",
        };
    }
}
