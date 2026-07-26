// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Shitcode.Heretic.SpriteOverlay;
using Content.Shared._Goobstation.Heretic.Components;
using Content.Shared._Goobstation.Wizard.Traps;
using Content.Shared._Shitcode.Heretic.Components;
using Content.Shared._Shitcode.Heretic.SpriteOverlay;
using Content.Shared._Shitcode.Heretic.Systems;
using Content.Shared.Heretic;
using Content.Pirate.Common.Sprite; // Pirate: viewcone visibility modifiers
using Robust.Client.GameObjects;

namespace Content.Client._Shitcode.Heretic;

public sealed class ShadowCloakSystem : SharedShadowCloakSystem
{
    [Dependency] private readonly CommonSpriteVisibilitySystem _spriteVis = default!; // Pirate: viewcone
    [Dependency] private readonly AppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<EntropicPlumeAffectedComponent>>(UpdateOverlay);
        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<FireBlastedComponent>>(UpdateOverlay);
        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<HereticCombatMarkComponent>>(UpdateOverlay);
        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<IceCubeComponent>>(UpdateOverlay);
        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<StarMarkComponent>>(UpdateOverlay);
        SubscribeLocalEvent<ShadowCloakedComponent, SpriteOverlayUpdatedEvent<VoidCurseComponent>>(UpdateOverlay);

        SubscribeLocalEvent<ShadowCloakEntityComponent, ComponentStartup>(OnEntityStartup);
    }

    private void OnEntityStartup(Entity<ShadowCloakEntityComponent> ent, ref ComponentStartup args)
    {
        if (!Exists(ent.Comp.User))
            return;

        // Update visual appearance
        if (TryComp(ent.Comp.User.Value, out SpriteComponent? sprite))
            _appearance.OnChangeData(ent.Comp.User.Value, sprite);
    }

    private void UpdateOverlay<T>(Entity<ShadowCloakedComponent> ent, ref SpriteOverlayUpdatedEvent<T> args)
        where T : BaseSpriteOverlayComponent
    {
        if (GetShadowCloakEntity(ent) is not { } cloak)
            return;

        if (args.Added)
            args.Sys.AddOverlay(cloak.Owner, args.Comp, ent);
        else
            args.Sys.RemoveOverlay(cloak.Owner, args.Comp);
    }

    protected override void Startup(Entity<ShadowCloakedComponent> ent)
    {
        base.Startup(ent);
        _spriteVis.UpdateVisibilityModifiers(ent, nameof(ShadowCloakedComponent), 0f);
    }

    protected override void Shutdown(Entity<ShadowCloakedComponent> ent)
    {
        base.Shutdown(ent);
        _spriteVis.UpdateVisibilityModifiers(ent, nameof(ShadowCloakedComponent), 1f);
    }
}
