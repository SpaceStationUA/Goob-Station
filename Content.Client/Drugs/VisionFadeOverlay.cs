// SPDX-License-Identifier: MIT

using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Drugs;

public sealed class VisionFadeOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "VisionFade";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _fadeShader;

    public float CurrentFadePower = 0.0f;

    private const float MaxFadePower = 100f;

    private const float FadePowerScale = 10f;

    private float _visualScale = 0;

    public VisionFadeOverlay()
    {
        IoCManager.InjectDependencies(this);
        _fadeShader = _prototypeManager.Index(Shader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        var statusSys = _sysMan.GetEntitySystem<Shared.StatusEffectNew.StatusEffectsSystem>();
        if (!statusSys.TryGetMaxTime<VisionFadeStatusEffectComponent>(playerEntity.Value, out var status))
            return;

        var time = status.Item2;

        var power = time == null ? MaxFadePower : (float) Math.Min((time - _timing.CurTime).Value.TotalSeconds, MaxFadePower);

        CurrentFadePower += FadePowerScale * (power - CurrentFadePower) * args.DeltaSeconds / (power + 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        // Quick ramp up: sharp onset fits eye pain, and avoids undercutting the
        // existing TemporaryBlindness that hits when the reagent dose is high
        _visualScale = Math.Clamp(CurrentFadePower / 20f, 0.0f, 1.0f);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _fadeShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _fadeShader.SetParameter("fadePower", _visualScale);
        handle.UseShader(_fadeShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
