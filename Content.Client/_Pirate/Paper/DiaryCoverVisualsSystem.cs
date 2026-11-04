// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Paper;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client._Pirate.Paper;

public sealed class DiaryCoverVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private const string ClosedState = "closed";
    private const string OpenState = "open";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiaryCoverComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DiaryCoverComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<DiaryCoverComponent, DiaryCoverChangedEvent>(OnChanged);
    }

    private void OnStartup(Entity<DiaryCoverComponent> ent, ref ComponentStartup args) => UpdateSprite(ent);

    private void OnHandleState(Entity<DiaryCoverComponent> ent, ref AfterAutoHandleStateEvent args) => UpdateSprite(ent);

    private void OnChanged(Entity<DiaryCoverComponent> ent, ref DiaryCoverChangedEvent args) => UpdateSprite(ent);

    private void UpdateSprite(Entity<DiaryCoverComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        Entity<SpriteComponent?> sprite = (ent.Owner, spriteComp);
        var open = ent.Comp.Open;
        _sprite.LayerSetRsiState(sprite, DiaryCoverLayers.Cover, open ? OpenState : ClosedState);
        _sprite.LayerSetVisible(sprite, DiaryCoverLayers.PageLeft, open && ent.Comp.PageLeft);
        _sprite.LayerSetVisible(sprite, DiaryCoverLayers.PageRight, open && ent.Comp.PageRight);
    }
}
