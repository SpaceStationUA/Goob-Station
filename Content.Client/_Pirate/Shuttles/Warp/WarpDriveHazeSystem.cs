// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Graphics;
using Content.Shared._Pirate.Shuttles.Warp;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Shuttles.Warp;

/// <summary>
///     Heat haze on the warp drive housing. While the field is up the sprite draws through the
///     <c>WarpDriveHaze</c> post-shader, so the air around the coils shimmers. The strength follows the
///     drive's state and heat; an idle drive draws normally.
/// </summary>
public sealed class WarpDriveHazeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> HazeShader = "WarpDriveHaze";

    /// <summary>
    ///     Stable id for this post-shader, so it can be replaced or removed without disturbing any
    ///     other shader the same sprite may carry.
    /// </summary>
    private const string PostShaderId = "warp-drive-haze";

    private ShaderInstance _shader = default!;

    public override void Initialize()
    {
        base.Initialize();

        _shader = _proto.Index(HazeShader).InstanceUnique();

        SubscribeLocalEvent<WarpDriveComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<WarpDriveComponent, BeforePostShaderRenderEvent>(OnRender);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WarpDriveComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var drive, out var sprite))
        {
            var active = drive.DriveState != WarpDriveState.Idle;
            if (active == _sprite.HasPostShader((uid, sprite), PostShaderId))
                continue;

            if (!active)
            {
                _sprite.RemovePostShader((uid, sprite), PostShaderId);
                continue;
            }

            // The screen texture flag is per-entry now: without it the fragment shader samples an
            // unbound texture and the shimmer reads as garbage. BeforeOutlines is the same stage
            // the other entity effects (stealth, aura) use.
            _sprite.SetPostShader((uid, sprite), new SpriteComponent.PostShaderArgs(PostShaderId, _shader)
            {
                GetScreenTexture = true,
                RaiseShaderEvent = true,
                Before = ContentPostShaderIds.BeforeOutlines,
            });
        }
    }

    private void OnShutdown(Entity<WarpDriveComponent> ent, ref ComponentShutdown args)
    {
        if (Terminating(ent.Owner) || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.RemovePostShader((ent.Owner, sprite), PostShaderId);
    }

    private void OnRender(Entity<WarpDriveComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Id != PostShaderId)
            return;

        _shader.SetParameter("uIntensity", Intensity(ent.Comp));
    }

    private static float Intensity(WarpDriveComponent drive)
    {
        return drive.DriveState switch
        {
            WarpDriveState.Spooling => 0.3f,
            WarpDriveState.Engaged => 0.45f + 0.35f * Math.Clamp(drive.Heat, 0f, 1f),
            WarpDriveState.InTransit => 1f,
            _ => 0f,
        };
    }
}
