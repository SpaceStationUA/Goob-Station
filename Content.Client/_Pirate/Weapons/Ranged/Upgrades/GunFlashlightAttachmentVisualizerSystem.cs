// SPDX-FileCopyrightText: 2026
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Weapons.Ranged.Upgrades;
using Content.Shared._Pirate.Weapons.Ranged.Upgrades.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;

namespace Content.Client._Pirate.Weapons.Ranged.Upgrades;

public sealed class GunFlashlightAttachmentVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private const string LayerKey = "gun_flashlight";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GunFlashlightAttachmentComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GunFlashlightAttachmentComponent, AppearanceChangeEvent>(OnAppearanceChange);
    }

    private void OnStartup(EntityUid uid, GunFlashlightAttachmentComponent component, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        EnsureLayer((uid, sprite), component);
        UpdateLayer((uid, sprite), component, attached: false, lightOn: false);
    }

    private void OnAppearanceChange(EntityUid uid, GunFlashlightAttachmentComponent component, ref AppearanceChangeEvent args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        if (!_appearance.TryGetData<bool>(uid, GunFlashlightVisuals.Attached, out var attached, args.Component))
            attached = false;

        if (!_appearance.TryGetData<bool>(uid, GunFlashlightVisuals.LightOn, out var lightOn, args.Component))
            lightOn = false;

        EnsureLayer((uid, sprite), component);
        UpdateLayer((uid, sprite), component, attached, lightOn);
    }

    private void EnsureLayer(Entity<SpriteComponent?> ent, GunFlashlightAttachmentComponent component)
    {
        if (_sprite.LayerMapTryGet(ent, LayerKey, out _, false))
            return;

        var index = _sprite.LayerMapReserve(ent, LayerKey);
        _sprite.LayerSetData(ent, index, new PrototypeLayerData
        {
            RsiPath = component.Sprite.ToString(),
            State = component.StateOff,
            Offset = component.Offset,
            Visible = false
        });
    }

    private void UpdateLayer(Entity<SpriteComponent?> ent, GunFlashlightAttachmentComponent component, bool attached, bool lightOn)
    {
        if (!_sprite.LayerMapTryGet(ent, LayerKey, out var index, false))
            return;

        _sprite.LayerSetData(ent, index, new PrototypeLayerData
        {
            RsiPath = component.Sprite.ToString(),
            State = lightOn ? component.StateOn : component.StateOff,
            Offset = component.Offset,
            Visible = attached
        });
    }
}
