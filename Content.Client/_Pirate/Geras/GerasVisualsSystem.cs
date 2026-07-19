// SPDX-FileCopyrightText: 2026 Pirate Station Contributors
//
// SPDX-License-Identifier: MIT

using System.Numerics;
using Content.Shared._Pirate.Geras;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Geras;

public sealed class GerasVisualsSystem : VisualizerSystem<GerasVisualsComponent>
{
    private static readonly ProtoId<ShaderPrototype> ColorShader = "GerasColorTint";

    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GerasVisualsComponent, ComponentInit>(OnComponentInit);
    }

    private void OnComponentInit(Entity<GerasVisualsComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Shader = _prototypeManager.Index(ColorShader).InstanceUnique();

        if (TryComp(ent, out SpriteComponent? sprite) &&
            AppearanceSystem.TryGetData<Color>(ent, GerasVisuals.Color, out var color))
        {
            ApplyColor(ent, sprite, color);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, GerasVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite ||
            !AppearanceSystem.TryGetData<Color>(uid, GerasVisuals.Color, out var color, args.Component))
        {
            return;
        }

        ApplyColor((uid, component), sprite, color);
    }

    private void ApplyColor(Entity<GerasVisualsComponent> ent, SpriteComponent sprite, Color color)
    {
        var shader = ent.Comp.Shader ??= _prototypeManager.Index(ColorShader).InstanceUnique();
        shader.SetParameter("tint_color", new Vector3(color.R, color.G, color.B));

        var layer = 0;
        foreach (var _ in sprite.AllLayers)
        {
            sprite.LayerSetShader(layer++, shader);
        }
    }
}
