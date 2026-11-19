// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Pirate.Shared.DeepSpaceScanner;
using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.DeepSpaceScanner;

public sealed partial class DeepSpaceScannerRadarSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    private const float FlashSeconds = 0.6f;
    private const float FlashRadius = 10f;

    private readonly Dictionary<EntityUid, (DeepSpaceScanEvent Scan, TimeSpan RealStart)> _scans = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DeepSpaceScanEvent>(OnScan);
        SubscribeLocalEvent<DeepSpaceScannerComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnScan(DeepSpaceScanEvent ev)
    {
        if (!TryGetEntity(ev.Scanner, out var scanner))
            return;

        // A late scan sent to a new viewer is anchored to the original time, so the wave may have passed.
        var age = _timing.CurTime - ev.ScanTime;
        if (age < TimeSpan.Zero)
            age = TimeSpan.Zero;

        _scans[scanner.Value] = (ev, _timing.RealTime - age);
    }

    private void OnShutdown(Entity<DeepSpaceScannerComponent> ent, ref ComponentShutdown args)
    {
        _scans.Remove(ent.Owner);
    }

    public void Draw(DrawingHandleScreen handle, EntityUid console, Matrix3x2 worldToView, float minimapScale)
    {
        if (!_scans.TryGetValue(console, out var entry) ||
            !TryComp<DeepSpaceScannerComponent>(console, out var comp) ||
            comp.Range <= 0f)
        {
            return;
        }

        var scan = entry.Scan;
        var elapsed = (float) (_timing.RealTime - entry.RealStart).TotalSeconds;
        var waveSeconds = MathF.Max((float) comp.WaveDuration.TotalSeconds, 0.01f);
        var fadeEndSeconds = (float) (comp.ScanInterval - comp.FadeEndBeforeScan).TotalSeconds;
        var origin = Vector2.Transform(scan.Origin, worldToView);

        if (elapsed < waveSeconds)
        {
            var progress = Math.Clamp(elapsed / waveSeconds, 0f, 1f);
            var radius = comp.Range * progress * minimapScale;
            var alpha = 0.8f * (1f - progress * progress);
            for (var i = 0; i < 4; i++)
            {
                var ringRadius = radius - i * 2f;
                if (ringRadius <= 0f)
                    break;

                handle.DrawCircle(origin, ringRadius, comp.Color.WithAlpha(alpha * (1f - i / 4f)), false);
            }
        }

        foreach (var ping in scan.Pings)
        {
            var revealAt = (ping.Position - scan.Origin).Length() / comp.Range * waveSeconds;
            if (elapsed < revealAt)
                continue;

            var since = elapsed - revealAt;
            var fade = Math.Clamp(since / MathF.Max(fadeEndSeconds - revealAt, 0.1f), 0f, 1f);
            var alpha = 1f + (comp.FadedAlpha - 1f) * fade;
            if (ping.OtherLevel)
                alpha *= comp.OtherLevelAlpha;

            if (alpha <= 0f)
                continue;

            var position = Vector2.Transform(ping.Position, worldToView);
            var size = comp.DotRadius;
            handle.DrawCircle(position, size, comp.Color.WithAlpha(comp.Color.A * alpha));

            if (since < FlashSeconds)
            {
                var flash = since / FlashSeconds;
                handle.DrawCircle(position, size + FlashRadius * flash, comp.Color.WithAlpha(alpha * (1f - flash)), false);
            }
        }
    }
}
