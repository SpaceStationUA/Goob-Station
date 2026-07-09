using Content.Pirate.Shared.Blinking;
using Content.Shared.Bed.Sleep;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mobs.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Client.Blinking;

/// <summary>
/// Client-side blink visuals using transient, skin-tinted eyelid layers.
/// Passive blinks stay local; blink emotes only schedule visible client effects.
/// </summary>
public sealed class BlinkingSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeAllEvent<BlinkEffectEvent>(OnBlinkEffect);
    }

    // Cosmetic animation: render-frame time avoids predicted Update advancing timers repeatedly.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        var query = AllEntityQuery<BlinkingComponent, SpriteComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var blink, out var sprite, out var humanoid))
        {
            if (!blink.Enabled)
            {
                RemoveAllEyelids(uid, sprite);
                continue;
            }

            var async = IsAsync(blink);

            // Stagger first blinks so crowds do not synchronize.
            if (!EnsureComp<BlinkingVisualsComponent>(uid, out var visuals))
                visuals.MasterDelay = NextDelay(blink);

            UpdateEntity(uid, blink, sprite, humanoid, visuals, async, frameTime);
        }
    }

    private void UpdateEntity(
        EntityUid uid,
        BlinkingComponent blink,
        SpriteComponent sprite,
        HumanoidAppearanceComponent humanoid,
        BlinkingVisualsComponent visuals,
        bool async,
        float frameTime)
    {
        // Missing or hidden eyes should not keep stale eyelid layers.
        if (!TryGetEyelidColor(uid, sprite, humanoid, out var color))
        {
            RemoveAllEyelids(uid, sprite);
            return;
        }

        visuals.EyelidColor = color;

        // Closed-eye states hold eyelids shut and pause passive blinking.
        if (HasComp<SleepingComponent>(uid) ||
            (TryComp<EyeClosingComponent>(uid, out var closing) && closing.EyesClosed))
        {
            HoldClosed(uid, sprite, blink, async, color);
            return;
        }

        // Dead mobs never blink; CloseOnDeath controls whether lids stay shut.
        if (_mobState.IsDead(uid))
        {
            if (blink.CloseOnDeath)
                HoldClosed(uid, sprite, blink, async, color);
            else
                RemoveAllEyelids(uid, sprite);

            return;
        }

        visuals.MasterDelay -= frameTime;
        if (visuals.MasterDelay <= 0f && AllEyesIdle(visuals, async))
            LaunchEvent(blink, visuals, async);

        if (async)
        {
            AnimateEye(uid, sprite, blink, visuals, true, 0, BlinkingVisualsComponent.LeftKey, frameTime);
            AnimateEye(uid, sprite, blink, visuals, true, 1, BlinkingVisualsComponent.RightKey, frameTime);
        }
        else
        {
            AnimateEye(uid, sprite, blink, visuals, false, 0, BlinkingVisualsComponent.UnifiedKey, frameTime);
        }
    }

    private void LaunchEvent(BlinkingComponent blink, BlinkingVisualsComponent visuals, bool async)
    {
        var rapid = visuals.RapidActive;
        var duration = (float) (rapid ? blink.RapidDuration : blink.Duration).TotalSeconds;

        if (async)
        {
            // One async eye leads; the other trails by up to AsyncOffset.
            var leftLeads = _random.Prob(0.5f);
            var offset = _random.NextFloat() * (float) blink.AsyncOffset.TotalSeconds;
            ArmEye(visuals, 0, leftLeads ? 0f : offset, duration);
            ArmEye(visuals, 1, leftLeads ? offset : 0f, duration);
        }
        else
        {
            ArmEye(visuals, 0, 0f, duration);
        }

        // Rapid bursts use fixed spacing; idle blinks use randomized spacing.
        if (visuals.RapidEventsLeft > 0)
        {
            visuals.RapidEventsLeft--;
            visuals.RapidActive = true;
            visuals.MasterDelay = (float) blink.RapidInterval.TotalSeconds;
        }
        else
        {
            visuals.RapidActive = false;
            visuals.MasterDelay = NextDelay(blink);
        }
    }

    private static void ArmEye(BlinkingVisualsComponent visuals, int eye, float startDelay, float duration)
    {
        visuals.Pending[eye] = true;
        visuals.StartDelay[eye] = startDelay;
        visuals.CurrentDuration[eye] = duration;
        visuals.Progress[eye] = -1f;
    }

    private void AnimateEye(
        EntityUid uid,
        SpriteComponent sprite,
        BlinkingComponent blink,
        BlinkingVisualsComponent visuals,
        bool async,
        int eye,
        string layerKey,
        float frameTime)
    {
        if (visuals.Progress[eye] < 0f)
        {
            // No visible eyelid until this eye actually starts closing.
            if (!visuals.Pending[eye])
            {
                RemoveEyelid(uid, sprite, layerKey);
                return;
            }

            visuals.StartDelay[eye] -= frameTime;
            if (visuals.StartDelay[eye] > 0f)
            {
                RemoveEyelid(uid, sprite, layerKey);
                return;
            }

            visuals.Pending[eye] = false;
            visuals.Progress[eye] = 0f;
            EnsureEyelidLayer(uid, sprite, blink, async, layerKey);
        }

        visuals.Progress[eye] += frameTime;
        var duration = MathF.Max(visuals.CurrentDuration[eye], 0.01f);

        if (visuals.Progress[eye] >= duration)
        {
            visuals.Progress[eye] = -1f;
            RemoveEyelid(uid, sprite, layerKey);
            return;
        }

        // Wipe closed at the midpoint, then immediately wipe open.
        var coverage = EyelidCoverage(visuals.Progress[eye], duration);
        ShowEyelid(uid, sprite, layerKey, visuals.EyelidColor, coverage, blink.WipeDirection);
    }

    private void OnBlinkEffect(BlinkEffectEvent ev)
    {
        var uid = GetEntity(ev.Target);
        if (Deleted(uid) || !TryComp<BlinkingComponent>(uid, out var blink) || !blink.Enabled)
            return;

        var visuals = EnsureComp<BlinkingVisualsComponent>(uid);

        // Fire when idle, or on the next open frame.
        visuals.RapidActive = ev.Rapid;
        visuals.RapidEventsLeft = ev.Rapid ? Math.Max(blink.RapidCount - 1, 0) : 0;
        visuals.MasterDelay = 0f;
    }

    private static bool AllEyesIdle(BlinkingVisualsComponent visuals, bool async)
    {
        if (visuals.Pending[0] || visuals.Progress[0] >= 0f)
            return false;
        if (async && (visuals.Pending[1] || visuals.Progress[1] >= 0f))
            return false;
        return true;
    }

    private bool TryGetEyelidColor(EntityUid uid, SpriteComponent sprite, HumanoidAppearanceComponent humanoid, out Color color)
    {
        color = default;

        if (!_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out var eyesIndex, false))
            return false;

        // Hidden eyes include equipment, closed-eye states, and removed eye organs.
        if (!sprite[eyesIndex].Visible)
            return false;

        var rsi = _sprite.LayerGetEffectiveRsi((uid, sprite), eyesIndex);
        if (rsi == null)
            return false;

        if (!_sprite.LayerGetRsiState((uid, sprite), eyesIndex).IsValid)
            return false;

        // Skin-tinted eyelids, slightly darkened.
        var skin = humanoid.SkinColor;
        color = new Color(skin.R * 0.85f, skin.G * 0.85f, skin.B * 0.85f, 1f);
        return true;
    }

    private void EnsureEyelidLayer(EntityUid uid, SpriteComponent sprite, BlinkingComponent blink, bool async, string key)
    {
        if (_sprite.LayerExists((uid, sprite), key))
            return;

        if (!_sprite.LayerMapTryGet((uid, sprite), HumanoidVisualLayers.Eyes, out var eyesIndex, false))
            return;

        int index;
        if (async)
        {
            var stateName = key == BlinkingVisualsComponent.LeftKey ? blink.EyelidStateLeft : blink.EyelidStateRight;
            if (stateName == null || blink.EyelidRsi == null)
                return;

            index = _sprite.AddRsiLayer((uid, sprite), stateName, blink.EyelidRsi.Value, EyelidInsertIndex(uid, sprite, eyesIndex));
        }
        else
        {
            var rsi = _sprite.LayerGetEffectiveRsi((uid, sprite), eyesIndex);
            var state = _sprite.LayerGetRsiState((uid, sprite), eyesIndex);
            if (rsi == null || !state.IsValid)
                return;

            index = _sprite.AddRsiLayer((uid, sprite), state, rsi, EyelidInsertIndex(uid, sprite, eyesIndex));
        }

        _sprite.LayerMapSet((uid, sprite), key, index);
        sprite.LayerSetShader(index, _prototype.Index(EyelidShader).InstanceUnique(), EyelidShader.Id);
        _sprite.LayerSetVisible((uid, sprite), index, false);
    }

    /// <summary>
    /// Inserts eyelids above eye markings but below the next base humanoid layer.
    /// </summary>
    private int EyelidInsertIndex(EntityUid uid, SpriteComponent sprite, int eyesIndex)
    {
        var insert = int.MaxValue;
        foreach (var layer in Enum.GetValues<HumanoidVisualLayers>())
        {
            if (_sprite.LayerMapTryGet((uid, sprite), layer, out var idx, false) && idx > eyesIndex && idx < insert)
                insert = idx;
        }

        return insert == int.MaxValue ? eyesIndex + 1 : insert;
    }

    private void ShowEyelid(EntityUid uid, SpriteComponent sprite, string layerKey, Color color, float coverage = 1f, BlinkingWipeDirection wipeDirection = BlinkingWipeDirection.TopDown)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerKey, out var index, false))
            return;

        if (_sprite.TryGetLayer((uid, sprite), index, out var layer, false))
        {
            layer.Shader?.SetParameter(EyelidCoverageParameter, Math.Clamp(coverage, 0f, 1f));
            layer.Shader?.SetParameter(EyelidWipeDirectionParameter, (float) wipeDirection);
        }
        _sprite.LayerSetColor((uid, sprite), index, color);
        _sprite.LayerSetVisible((uid, sprite), index, true);
    }

    /// <summary>
    /// Removes an eyelid layer; the map key must go first because RemoveLayer mutates layer maps.
    /// </summary>
    private void RemoveEyelid(EntityUid uid, SpriteComponent sprite, string layerKey)
    {
        if (!_sprite.LayerMapTryGet((uid, sprite), layerKey, out var index, false))
            return;

        _sprite.LayerMapRemove((uid, sprite), layerKey);
        _sprite.RemoveLayer((uid, sprite), index, false);
    }

    private void HoldClosed(EntityUid uid, SpriteComponent sprite, BlinkingComponent blink, bool async, Color color)
    {
        if (async)
        {
            EnsureEyelidLayer(uid, sprite, blink, true, BlinkingVisualsComponent.LeftKey);
            EnsureEyelidLayer(uid, sprite, blink, true, BlinkingVisualsComponent.RightKey);
            ShowEyelid(uid, sprite, BlinkingVisualsComponent.LeftKey, color);
            ShowEyelid(uid, sprite, BlinkingVisualsComponent.RightKey, color);
        }
        else
        {
            EnsureEyelidLayer(uid, sprite, blink, false, BlinkingVisualsComponent.UnifiedKey);
            ShowEyelid(uid, sprite, BlinkingVisualsComponent.UnifiedKey, color);
        }
    }

    private void RemoveAllEyelids(EntityUid uid, SpriteComponent sprite)
    {
        RemoveEyelid(uid, sprite, BlinkingVisualsComponent.UnifiedKey);
        RemoveEyelid(uid, sprite, BlinkingVisualsComponent.LeftKey);
        RemoveEyelid(uid, sprite, BlinkingVisualsComponent.RightKey);
    }

    private static bool IsAsync(BlinkingComponent blink)
        => blink.Asynchronous && blink.EyelidRsi != null && blink.EyelidStateLeft != null && blink.EyelidStateRight != null;

    private static readonly ProtoId<ShaderPrototype> EyelidShader = "PirateBlinkEyelid";
    private const string EyelidCoverageParameter = "Coverage";
    private const string EyelidWipeDirectionParameter = "WipeDirection";

    /// <summary>
    /// Wipe coverage over one close+open blink. 0 = open, 1 = fully covered.
    /// </summary>
    private static float EyelidCoverage(float t, float duration)
    {
        var half = MathF.Max(duration * 0.5f, 0.0001f);
        var x = t < half ? t / half : (duration - t) / half;
        return Math.Clamp(x, 0f, 1f);
    }

    private float NextDelay(BlinkingComponent blink)
    {
        var min = (float) blink.MinInterval.TotalSeconds;
        var max = (float) blink.MaxInterval.TotalSeconds;
        return min + _random.NextFloat() * (max - min);
    }
}
