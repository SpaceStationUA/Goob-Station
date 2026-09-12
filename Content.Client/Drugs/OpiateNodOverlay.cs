// SPDX-License-Identifier: MIT

using Content.Shared.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Drugs;

public sealed class OpiateNodOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "OpiateNod";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _nodShader;

    public float CurrentNodPower = 0.0f;

    private const float MaxNodPower = 100f;

    private const float NodPowerScale = 8f;

    private float _visualScale = 0;

    public OpiateNodOverlay()
    {
        IoCManager.InjectDependencies(this);
        _nodShader = _prototypeManager.Index(Shader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        var statusSys = _sysMan.GetEntitySystem<Shared.StatusEffectNew.StatusEffectsSystem>();
        if (!statusSys.TryGetMaxTime<OpiateNodStatusEffectComponent>(playerEntity.Value, out var status))
            return;

        var time = status.Item2;

        var power = time == null ? MaxNodPower : (float) Math.Min((time - _timing.CurTime).Value.TotalSeconds, MaxNodPower);

        CurrentNodPower += NodPowerScale * (power - CurrentNodPower) * args.DeltaSeconds / (power + 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        _visualScale = Math.Clamp(CurrentNodPower / 50f, 0.0f, 1.0f);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _nodShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _nodShader.SetParameter("nodPower", _visualScale);
        handle.UseShader(_nodShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
