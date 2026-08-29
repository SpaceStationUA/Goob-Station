/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._Pirate.ZLevels.Core.Components;
using Content.Shared._Pirate.ZLevels.Core.EntitySystems;
using Content.Shared.Camera;
using Content.Shared.CCVar;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>
/// Client-side Z-level rendering and eye offset.
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

        SubscribeLocalEvent<CEZPhysicsComponent, AfterAutoHandleStateEvent>(OnZPhysicsHandleState);
        SubscribeLocalEvent<CEZPhysicsComponent, GetEyeOffsetEvent>(OnEyeOffset);

        InitializeVisuals();
    }

    private void OnEyeOffset(Entity<CEZPhysicsComponent> ent, ref GetEyeOffsetEvent args)
    {
        Angle rotation = _eye.CurrentEye.Rotation * -1;
        var renderHeight = GetRenderHeight((ent, ent), Transform(ent));
        var offset = rotation.RotateVec(new Vector2(0, renderHeight * ZLevelOffset));
        args.Offset += offset;
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
    }

    /// <summary>Returns raw simulated height, using a parent's height for riders.</summary>
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
