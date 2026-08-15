// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.PDA;
using Content.Shared._Pirate.CartridgeLoader.Cartridges;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.CartridgeLoader.Cartridges;

/// <summary>Adds the takeover screen to armed PDAs.</summary>
public sealed class DetomatixArmedVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly Vector2 LogoScale = new(0.5f, 0.5f);

    private const string OverlayRsi = "_Pirate/Objects/Devices/pda_detomatix.rsi";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DetomatixArmedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DetomatixArmedComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<DetomatixArmedComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite) || IsWideScreen((ent.Owner, sprite)))
            return;

        AddLayer((ent.Owner, sprite), DetomatixVisualLayers.Screen, "screen", null);
        AddLayer((ent.Owner, sprite), DetomatixVisualLayers.Logo, "screen_s", LogoScale);
    }

    private bool IsWideScreen(Entity<SpriteComponent> sprite)
    {
        if (!_sprite.LayerMapTryGet(sprite.AsNullable(), PdaVisualizerSystem.PdaVisualLayers.IdLight, out var index, false))
            return false;

        return _sprite.LayerGetRsiState(sprite.AsNullable(), index).Name?.EndsWith("_wide") ?? false;
    }

    private void OnShutdown(Entity<DetomatixArmedComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent) || !TryComp(ent, out SpriteComponent? sprite))
            return;

        // Highest index first: removing a layer shifts everything above it down one.
        RemoveLayer((ent.Owner, sprite), DetomatixVisualLayers.Logo);
        RemoveLayer((ent.Owner, sprite), DetomatixVisualLayers.Screen);
    }

    private void AddLayer(Entity<SpriteComponent> sprite, DetomatixVisualLayers key, string state, Vector2? scale)
    {
        if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out _, false))
            return;

        var index = _sprite.AddLayer(sprite.AsNullable(),
            new PrototypeLayerData
            {
                RsiPath = OverlayRsi,
                State = state,
                Scale = scale,
                Shader = "unshaded",
                Visible = true,
            },
            null);

        if (index < 0)
            return;

        _sprite.LayerMapSet(sprite.AsNullable(), key, index);
    }

    private void RemoveLayer(Entity<SpriteComponent> sprite, DetomatixVisualLayers key)
    {
        if (_sprite.LayerMapTryGet(sprite.AsNullable(), key, out var index, false))
            _sprite.RemoveLayer(sprite.AsNullable(), index);
    }

    private enum DetomatixVisualLayers : byte
    {
        Screen,
        Logo,
    }
}
