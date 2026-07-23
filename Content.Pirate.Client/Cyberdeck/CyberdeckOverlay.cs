// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Roudenn <romabond091@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Client.Cyberdeck;

public sealed class CyberdeckOverlay : Overlay
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _shader;

    private const float PulseRate = 3f;
    private const float Level = 0.7f;

    public CyberdeckOverlay()
    {
        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index<ShaderPrototype>("GradientCircleMask").InstanceUnique();
        ZIndex = 1;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_player.LocalEntity, out EyeComponent? eye)
            || args.Viewport.Eye != eye.Eye)
            return;

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;
        var adjustedTime = (float) _timing.RealTime.TotalSeconds * PulseRate;

        var outerMax = 2f * distance;
        var outerMin = 0.8f * distance;
        var innerMax = 0.6f * distance;
        var innerMin = 0.2f * distance;
        var outerRadius = outerMax - Level * (outerMax - outerMin);
        var innerRadius = innerMax - Level * (innerMax - innerMin);

        _shader.SetParameter("time", MathF.Max(0f, MathF.Sin(adjustedTime)));
        _shader.SetParameter("color", new Vector3(0f, 0f, 1f));
        _shader.SetParameter("darknessAlphaOuter", 0.8f);
        _shader.SetParameter("outerCircleRadius", outerRadius);
        _shader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
        _shader.SetParameter("innerCircleRadius", innerRadius);
        _shader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);

        handle.UseShader(_shader);
        handle.DrawRect(viewport, Color.White);
    }
}
