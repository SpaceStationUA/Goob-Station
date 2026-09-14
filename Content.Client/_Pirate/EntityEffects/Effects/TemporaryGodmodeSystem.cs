// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.EntityEffects.Effects;
using Robust.Client.GameObjects;

namespace Content.Client._Pirate.EntityEffects.Effects;

/// <summary>
/// Client side of the temporary godmode effect: tints the subject green while the
/// component is present and restores the original color when it goes away.
/// </summary>
public sealed class TemporaryGodmodeSystem : SharedTemporaryGodmodeSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryGodmodeComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<TemporaryGodmodeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<TemporaryGodmodeComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        ent.Comp.OldColor = sprite.Color;
        sprite.Color = ent.Comp.TintColor;
    }

    private void OnShutdown(Entity<TemporaryGodmodeComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (!TryComp(ent, out SpriteComponent? sprite))
            return;

        sprite.Color = ent.Comp.OldColor;
    }
}
