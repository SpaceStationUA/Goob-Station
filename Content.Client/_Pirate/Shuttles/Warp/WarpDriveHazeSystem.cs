// SPDX-License-Identifier: AGPL-3.0-or-later

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

    private static readonly ProtoId<ShaderPrototype> HazeShader = "WarpDriveHaze";

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
            var want = active ? _shader : null;

            if (sprite.PostShader == want)
                continue;

            sprite.PostShader = want;
            sprite.GetScreenTexture = active;
            sprite.RaiseShaderEvent = active;
        }
    }

    private void OnShutdown(Entity<WarpDriveComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite) && sprite.PostShader == _shader)
        {
            sprite.PostShader = null;
            sprite.GetScreenTexture = false;
            sprite.RaiseShaderEvent = false;
        }
    }

    private void OnRender(Entity<WarpDriveComponent> ent, ref BeforePostShaderRenderEvent args)
    {
        if (args.Sprite.PostShader != _shader)
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
