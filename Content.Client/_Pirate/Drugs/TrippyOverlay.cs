// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Drugs;

public sealed class TrippyOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "Trippy";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _trippyShader;

    public float CurrentTripPower = 0.0f;

    private const float MaxTripPower = 100f;

    private const float TripPowerScale = 8f;

    private float _visualScale = 0;

    public TrippyOverlay()
    {
        IoCManager.InjectDependencies(this);
        _trippyShader = _prototypeManager.Index(Shader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        var statusSys = _sysMan.GetEntitySystem<Shared.StatusEffectNew.StatusEffectsSystem>();
        if (!statusSys.TryGetMaxTime<TrippyStatusEffectComponent>(playerEntity.Value, out var status))
            return;

        var time = status.Item2;

        var power = time == null ? MaxTripPower : (float) Math.Min((time - _timing.CurTime).Value.TotalSeconds, MaxTripPower);

        CurrentTripPower += TripPowerScale * (power - CurrentTripPower) * args.DeltaSeconds / (power + 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        _visualScale = Math.Clamp(CurrentTripPower / 50f, 0.0f, 1.0f);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _trippyShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _trippyShader.SetParameter("tripPower", _visualScale);
        handle.UseShader(_trippyShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
