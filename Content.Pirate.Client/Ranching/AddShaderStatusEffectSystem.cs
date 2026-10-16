// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Ranching;
using Content.Shared.StatusEffectNew;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Ranching;

public sealed class AddShaderStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AddShaderStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<AddShaderStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<AddShaderStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        if (!TryComp<SpriteComponent>(args.Target, out var sprite))
            return;

        _sprite.SetPostShader((args.Target, sprite),
            new SpriteComponent.PostShaderArgs($"pirate-ranching:{ent.Owner}",
                _prototypes.Index<ShaderPrototype>(ent.Comp.Shader).InstanceUnique())
            {
                GetScreenTexture = true,
                RaiseShaderEvent = true,
            });
    }

    private void OnRemoved(Entity<AddShaderStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (TerminatingOrDeleted(args.Target) || !TryComp<SpriteComponent>(args.Target, out var sprite))
            return;

        _sprite.RemovePostShader((args.Target, sprite), $"pirate-ranching:{ent.Owner}");
    }
}
