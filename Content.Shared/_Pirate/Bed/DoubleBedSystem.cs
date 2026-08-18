using Content.Shared._Pirate.Bed.Components;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Placeable;
using Content.Shared.Tag;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using System.Numerics;

namespace Content.Shared._Pirate.Bed;

public sealed class DoubleBedSystem : EntitySystem
{
    [Dependency] private readonly TagSystem _tagSystem = default!;
    [Dependency] private readonly PlaceableSurfaceSystem _placeableSurface = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DoubleBedComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<DoubleBedComponent, StrapAttemptEvent>(OnStrapAttempt, before: new[] { typeof(SharedBuckleSystem) });
        SubscribeLocalEvent<DoubleBedComponent, UnstrappedEvent>(OnUnstrapped, after: new[] { typeof(SharedBuckleSystem) });
        SubscribeLocalEvent<DoubleBedComponent, AfterInteractUsingEvent>(OnAfterInteractUsing, before: new[] { typeof(PlaceableSurfaceSystem) });
        SubscribeLocalEvent<DoubleBedComponent, InteractHandEvent>(OnInteractHand, before: new[] { typeof(SharedBuckleSystem) });
    }

    private void OnInteractHand(Entity<DoubleBedComponent> ent, ref InteractHandEvent args)
    {
        if (args.ClickLocation is { } clickLocation && TryComp<StrapComponent>(ent, out var strap))
        {
            ent.Comp.PendingBuckleOffset = GetClosestOffset(GetLocalClickY(ent, clickLocation), ent.Comp);
            strap.BuckleOffset = ent.Comp.PendingBuckleOffset.Value;
            Dirty(ent, strap);
        }
    }

    private void OnStartup(Entity<DoubleBedComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<StrapComponent>(ent, out var strap) && strap.BuckleOffset == Vector2.Zero)
        {
            strap.BuckleOffset = ent.Comp.LeftOffset;
            Dirty(ent, strap);
        }
    }

    private void OnStrapAttempt(Entity<DoubleBedComponent> ent, ref StrapAttemptEvent args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        var offset = ent.Comp.PendingBuckleOffset ??
                     (IsOffsetOccupied(strap, ent.Comp.LeftOffset)
                         ? ent.Comp.RightOffset
                         : ent.Comp.LeftOffset);
        ent.Comp.PendingBuckleOffset = null;

        if (IsOffsetOccupied(strap, offset))
        {
            args.Cancelled = true;
            return;
        }

        strap.BuckleOffset = offset;
        Dirty(ent, strap);
    }

    private void OnUnstrapped(Entity<DoubleBedComponent> ent, ref UnstrappedEvent args)
    {
        if (!TryComp<StrapComponent>(ent, out var strap))
            return;

        strap.BuckleOffset = ent.Comp.LeftOffset;
        Dirty(ent, strap);
    }

    private void OnAfterInteractUsing(Entity<DoubleBedComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!TryComp<PlaceableSurfaceComponent>(ent, out var surface) || !_tagSystem.HasTag(args.Used, "Bedsheet"))
            return;

        if (HasComp<DoubleBedSheetComponent>(args.Used))
        {
            _placeableSurface.SetPositionOffset(ent, ent.Comp.RightBedsheetOffset, surface);
            return;
        }

        var side = GetClosestSide(GetLocalClickY(ent, args.ClickLocation), ent.Comp);

        _placeableSurface.SetPositionOffset(ent,
            side == BedSide.Left ? ent.Comp.LeftBedsheetOffset : ent.Comp.RightBedsheetOffset,
            surface);
    }

    private static Vector2 GetClosestOffset(float clickY, DoubleBedComponent component)
    {
        return GetClosestSide(clickY, component) == BedSide.Left
            ? component.LeftOffset
            : component.RightOffset;
    }

    private static BedSide GetClosestSide(float clickY, DoubleBedComponent component)
    {
        return MathF.Abs(clickY - component.LeftOffset.Y) < MathF.Abs(clickY - component.RightOffset.Y)
            ? BedSide.Left
            : BedSide.Right;
    }

    private float GetLocalClickY(Entity<DoubleBedComponent> bed, EntityCoordinates clickLocation)
    {
        var localClick = Vector2.Transform(
            _transform.ToMapCoordinates(clickLocation).Position,
            _transform.GetInvWorldMatrix(bed));
        return localClick.Y;
    }

    private bool IsOffsetOccupied(StrapComponent strap, Vector2 offset)
    {
        foreach (var buckled in strap.BuckledEntities)
        {
            if (!TryComp<TransformComponent>(buckled, out var transform))
                continue;

            if ((transform.LocalPosition - offset).LengthSquared() < 0.01f)
                return true;
        }

        return false;
    }

    private enum BedSide : byte
    {
        Left,
        Right,
    }
}
