// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client._Pirate.Photo;
using Content.Client.Stealth;
using Content.Pirate.Shared.Overlays;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Stealth.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Overlays;

public sealed class SharkVisionOverlay : Overlay
{
    [Dependency] private readonly IEntityManager _entity = default!;
    [Dependency] private readonly IEyeManager _eyeManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private readonly TransformSystem _transform;
    private readonly StealthSystem _stealth;
    private readonly ContainerSystem _container;
    private readonly SharedSolutionContainerSystem _solution;
    private readonly SpriteSystem _sprite;
    private readonly PhotoCaptureFilterSystem _photoCaptureFilter;
    private readonly EntityLookupSystem _lookup;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly List<SharkVisionRenderEntry> _entries = [];
    private readonly HashSet<EntityUid> _seen = [];
    private readonly HashSet<Entity<SolutionContainerManagerComponent>> _candidates = [];

    public SharkVisionComponent? Comp;

    public SharkVisionOverlay()
    {
        IoCManager.InjectDependencies(this);

        _container = _entity.System<ContainerSystem>();
        _transform = _entity.System<TransformSystem>();
        _stealth = _entity.System<StealthSystem>();
        _solution = _entity.System<SharedSolutionContainerSystem>();
        _sprite = _entity.System<SpriteSystem>();
        _photoCaptureFilter = _entity.System<PhotoCaptureFilterSystem>();
        _lookup = _entity.System<EntityLookupSystem>();

        ZIndex = -1;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        return args.Viewport.Eye == _eyeManager.CurrentEye;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_photoCaptureFilter.IsSuppressedForEye(args.Viewport.Eye, PhotoCaptureSuppressionMask.VisionEffects))
            return;

        if (Comp is null)
            return;

        var worldHandle = args.WorldHandle;
        var eye = args.Viewport.Eye;

        if (eye == null)
            return;

        var accumulator = Math.Clamp(Comp.PulseAccumulator, 0f, Comp.PulseTime);
        var alpha = Comp.PulseTime <= 0f ? 1f : float.Lerp(1f, 0f, accumulator / Comp.PulseTime);

        var mapId = eye.Position.MapId;
        var eyeRot = eye.Rotation;

        GetVisionEntities(Comp.BloodPrototypes, mapId, args.WorldAABB, eyeRot);

        foreach (var entry in _entries)
        {
            Render(entry.Ent, worldHandle, entry.EyeRot, Comp.Color, alpha);
        }

        worldHandle.SetTransform(Matrix3x2.Identity);
    }

    private void GetVisionEntities(ProtoId<ReagentPrototype>[] bloodPrototypes,
        MapId mapId,
        Box2 worldAabb,
        Angle eyeRot)
    {
        _entries.Clear();
        _seen.Clear();
        _candidates.Clear();

        // Limit the search to the viewport, including contained entities.
        _lookup.GetEntitiesIntersecting(mapId, worldAabb, _candidates);

        foreach (var candidate in _candidates)
        {
            if (!HasExposedBlood(candidate, bloodPrototypes))
                continue;

            var uid = candidate.Owner;

            if (!_entity.TryGetComponent<TransformComponent>(uid, out var xform))
                continue;

            // Draw contained blood on its carrier.
            _entity.TryGetComponent<SpriteComponent>(uid, out var sprite);

            // Highlight the outermost container.
            if (_container.TryGetOuterContainer(uid, xform, out var container)
                && _entity.TryGetComponent<SpriteComponent>(container.Owner, out var ownerSprite)
                && _entity.TryGetComponent<TransformComponent>(container.Owner, out var ownerXform))
            {
                uid = container.Owner;
                sprite = ownerSprite;
                xform = ownerXform;
            }

            if (sprite is null || !_seen.Add(uid) || xform.MapID != mapId || !CanSee(uid, sprite))
                continue;

            _entries.Add(new SharkVisionRenderEntry((uid, sprite, xform), eyeRot));
        }
    }

    private bool HasExposedBlood(Entity<SolutionContainerManagerComponent> ent, ProtoId<ReagentPrototype>[] bloodPrototypes)
    {
        string? bloodstream = null;
        string? bloodstreamTemporary = null;

        if (_entity.TryGetComponent<BloodstreamComponent>(ent.Owner, out var stream))
        {
            if (stream.BleedAmount > 0)
                return true;

            bloodstream = stream.BloodSolutionName;
            bloodstreamTemporary = stream.BloodTemporarySolutionName;
        }

        foreach (var name in ent.Comp.Containers)
        {
            if (name == bloodstream || name == bloodstreamTemporary)
                continue;

            if (!_solution.TryGetSolution((ent.Owner, ent.Comp), name, out _, out var solution))
                continue;

            foreach (var reagent in solution.GetReagentPrototypes(_proto).Keys)
            {
                foreach (var blood in bloodPrototypes)
                {
                    if (reagent.ID == blood)
                        return true;
                }
            }
        }

        return false;
    }

    private void Render(Entity<SpriteComponent, TransformComponent> ent,
        DrawingHandleWorld handle,
        Angle eyeRot,
        Color color,
        float alpha)
    {
        var (uid, sprite, xform) = ent;
        var position = _transform.GetWorldPosition(xform);
        var rotation = _transform.GetWorldRotation(xform);

        var originalColor = sprite.Color;
        _sprite.SetColor((uid, sprite), color.WithAlpha(alpha));
        _sprite.RenderSprite((uid, sprite), handle, eyeRot, rotation, position);
        _sprite.SetColor((uid, sprite), originalColor);
    }

    private bool CanSee(EntityUid uid, SpriteComponent sprite)
    {
        return sprite.Visible && (!_entity.TryGetComponent(uid, out StealthComponent? stealth) ||
                                  _stealth.GetVisibility(uid, stealth) > 0.5f);
    }
}

public record struct SharkVisionRenderEntry(
    Entity<SpriteComponent, TransformComponent> Ent,
    Angle EyeRot);
