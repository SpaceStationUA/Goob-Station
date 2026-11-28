using Content.Goobstation.Shared.Fax;
using Content.Goobstation.Shared.Paint;
using Content.Shared.Popups;

namespace Content.Goobstation.Server.Paint;

public sealed partial class PaintFaxSystem : EntitySystem
{
    [Dependency] private SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaintSurfaceComponent, GettingFaxedCopiedEvent>(OnFaxCopied);
        SubscribeLocalEvent<PaintSurfaceComponent, GettingFaxedSentEvent>(OnFaxSent);
    }

    private void OnFaxCopied(Entity<PaintSurfaceComponent> ent, ref GettingFaxedCopiedEvent args)
    {
        // Pirate: papers carry a paint surface as well, so only block the ones that actually have a drawing
        if (!HasDrawing(ent.Comp))
            return;

        args.Handled = true;

        if (args.Args.Actor.IsValid())
            _popup.PopupEntity(Loc.GetString("paint-fax-use-photocopier"), args.Fax, args.Args.Actor);
    }

    private void OnFaxSent(Entity<PaintSurfaceComponent> ent, ref GettingFaxedSentEvent args)
    {
        // Pirate: papers carry a paint surface as well, so only block the ones that actually have a drawing
        if (!HasDrawing(ent.Comp))
            return;

        args.Handled = true;

        if (args.Args.Actor.IsValid())
            _popup.PopupEntity(Loc.GetString("paint-fax-use-photocopier"), args.Fax, args.Args.Actor);
    }

    // Pirate: an untouched surface is filled with palette entry 0, so only non-zero pixels count as a drawing
    private static bool HasDrawing(PaintSurfaceComponent surface)
    {
        if (surface.Image is not { } image)
            return false;

        foreach (var pixel in image.Pixels)
        {
            if (pixel != 0)
                return true;
        }

        return false;
    }
}
