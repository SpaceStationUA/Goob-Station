// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Pirate.Common.Sprite;
using Robust.Client.GameObjects;

namespace Content.Pirate.Client.Sprite;

public sealed partial class SpriteVisibilitySystem : CommonSpriteVisibilitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private EntityQuery<SpriteComponent> _spriteQuery;

    public override void Initialize()
    {
        base.Initialize();

        _spriteQuery = GetEntityQuery<SpriteComponent>();

        SubscribeLocalEvent<SpriteVisibilityComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(Entity<SpriteVisibilityComponent> ent, ref ComponentStartup args)
    {
        if (!_spriteQuery.TryComp(ent, out var comp) || comp.Color.A >= 1f)
            return;

        ent.Comp.VisibilityModifiers[nameof(SpriteComponent)] = comp.Color.A;
    }

    public override void UpdateVisibilityModifiers(EntityUid uid, string key, float alpha)
    {
        if (!_spriteQuery.TryComp(uid, out var comp) || TerminatingOrDeleted(uid))
            return;

        if (alpha >= 1f)
            RemoveVisibilityModifier((uid, comp), key);
        else
            AddVisibilityModifier((uid, comp), key, alpha);
    }

    private void AddVisibilityModifier(Entity<SpriteComponent> ent, string key, float modifier)
    {
        var comp = EnsureComp<SpriteVisibilityComponent>(ent);
        modifier = MathF.Max(modifier, 0f);

        // Pirate perf: overlays re-apply the same alpha every frame; skip the no-op recalculation.
        if (comp.VisibilityModifiers.TryGetValue(key, out var existing) && existing.Equals(modifier))
            return;

        comp.VisibilityModifiers[key] = modifier;
        ReCalculateSpriteVisibility((ent, ent.Comp, comp));
    }

    private void RemoveVisibilityModifier(Entity<SpriteComponent?, SpriteVisibilityComponent?> ent, string key)
    {
        if (!Resolve(ent, ref ent.Comp1))
            return;

        if (!Resolve(ent, ref ent.Comp2, false))
        {
            // Pirate fix: no modifiers means we never touched this sprite — don't force alpha
            // back to 1 and clobber visibility state owned by other systems.
            return;
        }

        ent.Comp2.VisibilityModifiers.Remove(key);
        if (ent.Comp2.VisibilityModifiers.Count == 0)
        {
            RemCompDeferred(ent, ent.Comp2);
            SetSpriteVisibility(ent!, 1f);
            return;
        }

        if (ent.Comp2.VisibilityModifiers.Count == 1 &&
            ent.Comp2.VisibilityModifiers.TryGetValue(nameof(SpriteComponent), out var alpha))
        {
            RemCompDeferred(ent, ent.Comp2);
            SetSpriteVisibility(ent!, alpha);
            return;
        }

        ReCalculateSpriteVisibility(ent!);
    }

    private void SetSpriteVisibility(Entity<SpriteComponent> ent, float visibility)
    {
        var e = ent.AsNullable();
        visibility = Math.Clamp(visibility, 0f, 1f);
        var visible = visibility > 0f;
        _sprite.SetVisible(e, visible);
        if (visible)
            _sprite.SetColor(e, ent.Comp.Color.WithAlpha(visibility));
    }

    private void ReCalculateSpriteVisibility(Entity<SpriteComponent, SpriteVisibilityComponent> ent)
    {
        // Pirate perf: manual loop instead of LINQ Aggregate to avoid boxing the enumerator per call.
        var visibility = 1f;
        foreach (var modifier in ent.Comp2.VisibilityModifiers.Values)
        {
            visibility *= modifier;
        }

        SetSpriteVisibility(ent, visibility);
    }
}
