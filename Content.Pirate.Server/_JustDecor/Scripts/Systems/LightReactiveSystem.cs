using System;
using System.Numerics;
using Content.Pirate.Server._JustDecor.Scripts.Components;
using Content.Server.DeviceLinking.Systems;
using Content.Server.Hands.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Light.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Pirate.Server._JustDecor.Scripts.Systems;

public sealed class LightReactiveSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DeviceLinkSystem _deviceLink = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LightReactiveComponent, ComponentInit>(OnInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LightReactiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_timing.CurTime < comp.NextUpdate)
                continue;

            comp.NextUpdate = _timing.CurTime + TimeSpan.FromSeconds(comp.UpdateInterval);
            var lit = IsLitByMatchingLight(uid, comp);

            if (lit == comp.Active)
                continue;

            comp.Active = lit;

            if (lit)
            {
                if (comp.ReactSound != null)
                    _audio.PlayPvs(comp.ReactSound, uid);

                _deviceLink.InvokePort(uid, comp.OnPort);
            }
            else
            {
                _deviceLink.InvokePort(uid, comp.OffPort);
            }
        }
    }

    private void OnInit(EntityUid uid, LightReactiveComponent comp, ComponentInit args)
    {
        _deviceLink.EnsureSourcePorts(uid, comp.OnPort, comp.OffPort);
    }

    private bool IsLitByMatchingLight(EntityUid uid, LightReactiveComponent comp)
    {
        var targetCoords = _transform.ToMapCoordinates(Transform(uid).Coordinates);

        foreach (var candidate in _lookup.GetEntitiesInRange(targetCoords, comp.Range))
        {
            if (!TryComp<MobStateComponent>(candidate, out var mobState) || mobState.CurrentState == MobState.Dead)
                continue;

            if (!TryComp<HandsComponent>(candidate, out var hands))
                continue;

            var holderCoords = _transform.ToMapCoordinates(Transform(candidate).Coordinates);
            var toTarget = targetCoords.Position - holderCoords.Position;

            if (toTarget.LengthSquared() <= 0.0001f)
                continue;

            var facing = Transform(candidate).LocalRotation.GetDir().ToVec();
            if (facing == Vector2.Zero)
                facing = Vector2.UnitX;

            if (Vector2.Dot(Vector2.Normalize(facing), Vector2.Normalize(toTarget)) < comp.RequiredDot)
                continue;

            foreach (var held in _hands.EnumerateHeld((candidate, hands)))
            {
                if (!TryComp<HandheldLightComponent>(held, out var light) || !light.Activated)
                    continue;

                if (!TryComp<PointLightComponent>(held, out var pointLight) || !pointLight.Enabled)
                    continue;

                if (!IsColorMatch(pointLight.Color, comp.RequiredColor, comp.ColorTolerance))
                    continue;

                return true;
            }
        }

        return false;
    }

    private static bool IsColorMatch(Color actual, Color expected, float tolerance)
    {
        var delta = actual.RGBA - expected.RGBA;
        return delta.Length() <= tolerance;
    }
}
