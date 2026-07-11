/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Client.Damage.Systems;
using Content.Shared._DV.Carrying;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Content.Shared.Damage.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>
/// Only process Eye offset and drawdepth on clientside
/// </summary>
public sealed partial class CEClientZLevelsSystem : CESharedZLevelsSystem
{
    private bool _clientInitialized;

    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IEyeManager _eye = default!;
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    // Live cache of the zlevels.ce_render_offset cvar; both render paths read this static.
    public static float ZLevelOffset = 0.3f;

    public override void Initialize()
    {
        base.Initialize();

        if (_clientInitialized)
            return;

        _clientInitialized = true;
        _overlay.AddOverlay(new CEZLevelBlurOverlay());

        // Keep the static offset in sync with the cvar so it can be tuned live from the console.
        _cfg.OnValueChanged(CCVars.CEZLevelsRenderOffset, value => ZLevelOffset = value, invokeImmediately: true);

        SubscribeLocalEvent<CEZPhysicsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZPhysicsComponent, AfterAutoHandleStateEvent>(OnZPhysicsHandleState);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);
        SubscribeLocalEvent<CEZPhysicsComponent, CEZPhysicsActivationChangedEvent>(OnActivationChanged);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentStartup>(OnItemZPhysicsStartup);
        SubscribeLocalEvent<CEZItemPhysicsComponent, ComponentRemove>(OnItemZPhysicsRemove);
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var localPosition = GetVisualsLocalPosition((ent, ent), Transform(ent));
        var offset = rotation.RotateVec(new Vector2(0, localPosition * ZLevelOffset));
        args.Offset += offset;
    }

    private void OnStartup(Entity<CEZPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
    }

    private void OnZPhysicsHandleState(Entity<CEZPhysicsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (!ZDebugStairsEnabled ||
            _player.LocalEntity != ent.Owner)
        {
            return;
        }

        DebugZStairCsv(ent,
            "client_z_state_handle",
            $"state={args.State.GetType().Name},local={StairCsvFloat(ent.Comp.LocalPosition)},vel={StairCsvFloat(ent.Comp.Velocity)},current_z={ent.Comp.CurrentZLevel}",
            $"{args.State.GetType().Name}|{StairCsvFloat(ent.Comp.LocalPosition)}|{StairCsvFloat(ent.Comp.Velocity)}|{ent.Comp.CurrentZLevel}|{Transform(ent).ParentUid}|{Transform(ent).GridUid}|{Transform(ent).MapUid}");
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var uid in ActiveBodies)
        {
            if (!ZPhysQuery.TryComp(uid, out var zPhys) ||
                !TryComp<SpriteComponent>(uid, out var sprite) ||
                !TransformQuery.TryComp(uid, out var xform))
            {
                continue;
            }

            var localPosition = GetVisualsLocalPosition((uid, zPhys), xform);

            sprite.NoRotation = localPosition != 0 || zPhys.NoRotDefault;

            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }

        // Update StartOffset for entities with running fatigue animations
        // This allows animations to follow dynamic offset changes (e.g., from Z-levels system)
        var query2 = EntityQueryEnumerator<StaminaComponent, SpriteComponent, CEZPhysicsComponent>();
        while (query2.MoveNext(out var uid, out var stamina, out var sprite, out var zPhys))
        {
            if (!_animation.HasRunningAnimation(uid, StaminaSystem.StaminaAnimationKey))
                continue;

            // Track the live sprite position (including z-level shift), not the static default.
            stamina.StartOffset = sprite.Offset;
        }

        var itemQuery = EntityQueryEnumerator<CEZItemPhysicsComponent, SpriteComponent>();
        while (itemQuery.MoveNext(out var uid, out var zItem, out var sprite))
        {
            var localPosition = MathF.Max(zItem.LocalPosition, 0f);

            // Flat on its layer: leave the sprite alone (restore once) so the per-frame drive
            // doesn't fight the throw's own animation — the short-throw twitch.
            if (localPosition <= 0f)
            {
                if (zItem.VisualsApplied)
                {
                    sprite.NoRotation = zItem.NoRotDefault;
                    _sprite.SetOffset((uid, sprite), zItem.SpriteOffsetDefault);
                    _sprite.SetDrawDepth((uid, sprite), zItem.DrawDepthDefault);
                    zItem.VisualsApplied = false;
                }

                continue;
            }

            EnsureItemVisualDefaults((uid, zItem), sprite);

            sprite.NoRotation = true;
            _sprite.SetOffset((uid, sprite), zItem.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), (int)Shared.DrawDepth.DrawDepth.OverMobs);
            zItem.VisualsApplied = true;
        }

        var carriedQuery = EntityQueryEnumerator<BeingCarriedComponent, SpriteComponent, CEZPhysicsComponent>();
        while (carriedQuery.MoveNext(out var uid, out var carried, out var sprite, out var zPhys))
        {
            if (!ZPhysQuery.TryComp(carried.Carrier, out var carrierZ) ||
                !TransformQuery.TryComp(carried.Carrier, out var carrierXform))
            {
                continue;
            }

            var localPosition = GetVisualsLocalPosition((carried.Carrier, carrierZ), carrierXform);

            _sprite.SetOffset((uid, sprite), zPhys.SpriteOffsetDefault + new Vector2(0, localPosition * ZLevelOffset));
            _sprite.SetDrawDepth((uid, sprite), localPosition > 0 ? (int)Shared.DrawDepth.DrawDepth.OverMobs : zPhys.DrawDepthDefault);
        }
    }

    private void OnActivationChanged(Entity<CEZPhysicsComponent> ent, ref CEZPhysicsActivationChangedEvent args)
    {
        if (args.Active)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.NoRotation = ent.Comp.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), ent.Comp.DrawDepthDefault);
    }

    private void OnItemZPhysicsStartup(Entity<CEZItemPhysicsComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            EnsureItemVisualDefaults(ent, sprite);
    }

    private void OnItemZPhysicsRemove(Entity<CEZItemPhysicsComponent> ent, ref ComponentRemove args)
    {
        if (!ent.Comp.VisualsInitialized ||
            !TryComp<SpriteComponent>(ent, out var sprite))
        {
            return;
        }

        sprite.NoRotation = ent.Comp.NoRotDefault;
        _sprite.SetOffset((ent.Owner, sprite), ent.Comp.SpriteOffsetDefault);
        _sprite.SetDrawDepth((ent.Owner, sprite), ent.Comp.DrawDepthDefault);
    }

    private void EnsureItemVisualDefaults(Entity<CEZItemPhysicsComponent> ent, SpriteComponent sprite)
    {
        if (ent.Comp.VisualsInitialized)
            return;

        ent.Comp.NoRotDefault = sprite.NoRotation;
        ent.Comp.DrawDepthDefault = sprite.DrawDepth;
        ent.Comp.SpriteOffsetDefault = sprite.Offset;
        ent.Comp.VisualsInitialized = true;
    }


    public float GetVisualsLocalPosition(Entity<CEZPhysicsComponent?> ent, TransformComponent? xform = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return 0;
        if (!Resolve(ent, ref xform, false))
            return 0;

        var pos = ent.Comp.LocalPosition;

        if (xform.ParentUid != xform.MapUid && ZPhysQuery.TryComp(xform.ParentUid, out var parentZPhys))
            pos = parentZPhys.LocalPosition;

        return pos;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlay.RemoveOverlay<CEZLevelBlurOverlay>();
    }
}
