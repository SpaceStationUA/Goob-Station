// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Hands.EntitySystems;
using Content.Shared.SprayPainter.Components;

namespace Content.Shared._Pirate.Crayon;

public sealed class DecalRotationSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<DecalRotateRequestEvent>(OnRotateRequest);
        SubscribeLocalEvent<CrayonRotationComponent, CrayonDrawRotationEvent>(OnCrayonDrawRotation);
        SubscribeLocalEvent<CrayonRotationComponent, CrayonRotationMessage>(OnRotationMessage);
    }

    public bool CanRotate(EntityUid? held)
    {
        return HasComp<CrayonRotationComponent>(held)
            || TryComp<SprayPainterComponent>(held, out var painter) && painter.DecalMode == DecalPaintMode.Add;
    }

    private void OnRotateRequest(DecalRotateRequestEvent msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user
            || _hands.GetActiveItem(user) is not { } held
            || !CanRotate(held))
            return;

        if (TryComp<CrayonRotationComponent>(held, out var crayon))
        {
            SetRotation((held, crayon), crayon.Rotation - Angle.FromDegrees(90));
            return;
        }

        var painter = Comp<SprayPainterComponent>(held);
        painter.SelectedDecalAngle = (painter.SelectedDecalAngle - 90) % 360;
        Dirty(held, painter);
    }

    private void OnCrayonDrawRotation(Entity<CrayonRotationComponent> ent, ref CrayonDrawRotationEvent args)
    {
        args.Rotation = ent.Comp.Rotation;
    }

    private void OnRotationMessage(Entity<CrayonRotationComponent> ent, ref CrayonRotationMessage args)
    {
        SetRotation(ent.AsNullable(), Angle.FromDegrees(args.Degrees));
    }

    /// <summary>
    /// Both the hotkey and the numeric controls set this same networked angle.
    /// </summary>
    public bool SetRotation(Entity<CrayonRotationComponent?> ent, Angle rotation)
    {
        if (!double.IsFinite(rotation.Theta) || !Resolve(ent, ref ent.Comp, false))
            return false;

        ent.Comp.Rotation = rotation.Reduced();
        Dirty(ent);
        return true;
    }
}
