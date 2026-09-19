// SPDX-License-Identifier: MIT

using Content.Shared._Pirate.Drugs;
using Content.Shared.StatusEffectNew;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Drugs;

public sealed class BadTripOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> Shader = "BadTrip";

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IEntitySystemManager _sysMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;
    public override bool RequestScreenTexture => true;
    private readonly ShaderInstance _badTripShader;

    public float CurrentBadTripPower = 0.0f;

    private const float MaxBadTripPower = 100f;

    private const float BadTripPowerScale = 8f;

    private float _visualScale = 0;

    public BadTripOverlay()
    {
        IoCManager.InjectDependencies(this);
        _badTripShader = _prototypeManager.Index(Shader).InstanceUnique();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        var playerEntity = _playerManager.LocalEntity;

        if (playerEntity == null)
            return;

        var statusSys = _sysMan.GetEntitySystem<Shared.StatusEffectNew.StatusEffectsSystem>();
        if (!statusSys.TryGetMaxTime<BadTripStatusEffectComponent>(playerEntity.Value, out var status))
            return;

        var time = status.Item2;

        var power = time == null ? MaxBadTripPower : (float) Math.Min((time - _timing.CurTime).Value.TotalSeconds, MaxBadTripPower);

        CurrentBadTripPower += BadTripPowerScale * (power - CurrentBadTripPower) * args.DeltaSeconds / (power + 1);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return false;

        if (args.Viewport.Eye != eyeComp.Eye)
            return false;

        _visualScale = Math.Clamp(CurrentBadTripPower / 30f, 0.0f, 1.0f);
        return _visualScale > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null)
            return;

        var handle = args.WorldHandle;
        _badTripShader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _badTripShader.SetParameter("badTripPower", _visualScale);
        handle.UseShader(_badTripShader);
        handle.DrawRect(args.WorldBounds, Color.White);
        handle.UseShader(null);
    }
}
