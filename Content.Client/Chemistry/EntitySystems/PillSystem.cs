// SPDX-License-Identifier: MIT

using Content.Shared.Chemistry.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility; // Pirate

namespace Content.Client.Chemistry.EntitySystems;

public sealed class PillSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath DefaultPillsRsi = new("/Textures/Objects/Specific/Chemistry/pills.rsi"); // Pirate

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PillComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    private void OnHandleState(EntityUid uid, PillComponent component, ref AfterAutoHandleStateEvent args)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        if (!_sprite.TryGetLayer((uid, sprite), 0, out var layer, false))
            return;

        // Pirate: Only apply pill-type visual override when using the default pills RSI.
        // Entities with custom RSIs (e.g. _DEN pills) set their own sprite state
        // and should not be overridden with pill{N} states.
        var effectiveRsi = _sprite.LayerGetEffectiveRsi((uid, sprite), 0);
        if (effectiveRsi == null || effectiveRsi.Path != DefaultPillsRsi)
            return;

        _sprite.LayerSetRsiState(layer, $"pill{component.PillType + 1}");
    }
}