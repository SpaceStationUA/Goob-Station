// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Client.Parallax.Managers;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Movement.Systems;
using Robust.Client.Player;
using Robust.Shared.Configuration;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Timing;

namespace Content.Client.Parallax;

/// <summary>
///     Shakes the local camera while the ship is warping. The offset goes through
///     <see cref="GetEyeOffsetEvent"/>, the channel the recoil system uses, so it stacks with recoil
///     and honours the screen shake cvar.
/// </summary>
/// <remarks>
///     Recoil is built for impacts and decays, so a warp needs its own sustained offset that follows
///     the speed.
/// </remarks>
public sealed class WarpShakeSystem : EntitySystem
{
    [Dependency] private readonly IParallaxManager _parallax = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;

    // Metres at full throttle. A tile is one metre, and an explosion's kick clamps at one.
    private const float MaxAmplitude = 0.06f;

    // Base frequency in Hz. Two waves per axis at different rates keep it from reading as one clean oscillation.
    private const float Frequency = 5.5f;

    private const float ImpulseDecay = 0.35f;

    private float _intensity = 1f;

    private Vector2 _shake;
    private bool _shaking;
    private bool _wasWarping;
    private double _impulseAt = double.MinValue;
    private float _impulseStrength;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EyeComponent, GetEyeOffsetEvent>(OnGetEyeOffset);
        Subs.CVar(_config, CCVars.ScreenShakeIntensity, OnIntensityChanged, true);
    }

    private void OnIntensityChanged(float value)
    {
        _intensity = value;
    }

    private void OnGetEyeOffset(Entity<EyeComponent> ent, ref GetEyeOffsetEvent args)
    {
        if (_shake == Vector2.Zero)
            return;

        if (ent.Owner != _player.LocalEntity)
            return;

        args.Offset += _shake;
    }

    public override void Update(float frameTime)
    {
        var local = _player.LocalEntity;
        if (local == null || !TryComp<EyeComponent>(local.Value, out var eye))
        {
            _shake = Vector2.Zero;
            _shaking = false;
            return;
        }

        var now = _timing.CurTime.TotalSeconds;
        var warping = _parallax.IsWarping;

        // A jump starts with a jolt and ends with one, so it reads as starting and stopping.
        if (warping && !_wasWarping)
            Impulse(now, 0.9f);
        else if (!warping && _wasWarping)
            Impulse(now, 1.2f);
        _wasWarping = warping;

        // Weighted toward the peak: gentle during the spool, strongest at the coast.
        var throttle = _parallax.WarpSpeedFraction;
        var shaped = throttle * (1f + 2f * throttle * throttle);
        var amplitude = MaxAmplitude * _intensity * shaped;

        var since = (float) (now - _impulseAt);
        if (since < 1.5f)
            amplitude += MaxAmplitude * _intensity * _impulseStrength * MathF.Exp(-since / ImpulseDecay);

        if (amplitude <= 0.0005f)
        {
            if (!_shaking)
                return;

            // Write the offset back without the shake, so it does not stick once the warp is over.
            _shake = Vector2.Zero;
            _shaking = false;
            _eye.UpdateEyeOffset((local.Value, eye));
            return;
        }

        var t = (float) now * Frequency;
        var noise = new Vector2(
            MathF.Sin(t) * 0.6f + MathF.Sin(t * 1.9f + 1.7f) * 0.4f,
            MathF.Cos(t * 0.83f + 0.4f) * 0.6f + MathF.Cos(t * 2.3f + 2.1f) * 0.4f);

        // The offset is in world space, so turn it by the eye to keep the shake screen-aligned.
        var rot = Matrix3x2.CreateRotation((float) (double) eye.Rotation);
        _shake = Vector2.Transform(noise * amplitude, rot);
        _shaking = true;

        // Unlike recoil, nothing re-raises the offset on its own while the shake runs.
        _eye.UpdateEyeOffset((local.Value, eye));
    }

    private void Impulse(double now, float strength)
    {
        _impulseAt = now;
        _impulseStrength = strength;
    }
}
