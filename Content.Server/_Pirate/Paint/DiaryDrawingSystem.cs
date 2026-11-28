// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using Content.Goobstation.Shared.Paint;
using Content.Shared._Pirate.Paper;
using Content.Shared.Paper;

namespace Content.Server._Pirate.Paint;

/// <summary>
/// Pirate: diary drawings - every leaf of a paginated document keeps its own chalk layer
/// inside <see cref="PaperPage.Drawing"/>, so a doodle belongs to the page it was made on
/// and follows that page when leaves are added, torn out or written to.
/// The live <see cref="PaintSurfaceComponent"/> only ever shows the leaf currently on
/// screen: the layer is saved into the leaf being left and loaded from the leaf being
/// opened. Because both the leaf's text and its drawing live in the flat
/// <see cref="PaperComponent.Content"/> string, the existing text persistence saves them.
/// </summary>
public sealed class DiaryDrawingSystem : EntitySystem
{
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedPaintSurfaceSystem _paint = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperPagesComponent, PaperPageChangingEvent>(OnPageChanging);
        SubscribeLocalEvent<PaperPagesComponent, PaperPageChangedEvent>(OnPageChanged);
        SubscribeLocalEvent<PaintSurfaceComponent, PaperDrawingDetachedEvent>(OnDrawingDetached);
        SubscribeLocalEvent<PaintSurfaceComponent, PaperDrawingAttachingEvent>(OnDrawingAttaching);
    }

    /// <summary>
    /// Pirate: diary drawings - a leaf torn out comes back as a real sheet, so the sheet gets
    /// the very same chalk layer the leaf held.
    /// </summary>
    private void OnDrawingDetached(Entity<PaintSurfaceComponent> ent, ref PaperDrawingDetachedEvent args)
    {
        if (args.Drawing.Length == 0)
            return;

        if (PaintImageCodec.Decode(args.Drawing, ent.Comp.Size) is not { } image)
            return;

        _paint.SetImage(ent, image);
    }

    /// <summary>
    /// Pirate: diary drawings - a drawn sheet tucked in as a leaf hands its chalk layer over
    /// to the new leaf; the sheet itself is consumed right after.
    /// </summary>
    private void OnDrawingAttaching(Entity<PaintSurfaceComponent> ent, ref PaperDrawingAttachingEvent args)
    {
        if (ent.Comp.Image is not { } image || !HasDrawing(image))
            return;

        args.Drawing = PaintImageCodec.Encode(image);
    }

    private void OnPageChanging(Entity<PaperPagesComponent> ent, ref PaperPageChangingEvent args)
    {
        if (!TryComp<PaperComponent>(ent.Owner, out var paper)
            || !TryComp<PaintSurfaceComponent>(ent.Owner, out var surface))
            return;

        StoreLeaf((ent.Owner, paper), (ent.Owner, surface), ent.Comp, args.Page);
    }

    private void OnPageChanged(Entity<PaperPagesComponent> ent, ref PaperPageChangedEvent args)
    {
        if (!TryComp<PaintSurfaceComponent>(ent.Owner, out var surface))
            return;

        LoadLeaf((ent.Owner, surface), ent.Comp, args.Page);
    }

    /// <summary>
    /// Pirate: diary drawings - saves the chalk layer currently on screen into the leaf it
    /// belongs to, so the flat content written to the database carries every page's doodle.
    /// </summary>
    public void Flush(EntityUid uid)
    {
        if (!TryComp<PaperPagesComponent>(uid, out var pages)
            || !TryComp<PaperComponent>(uid, out var paper)
            || !TryComp<PaintSurfaceComponent>(uid, out var surface))
            return;

        StoreLeaf((uid, paper), (uid, surface), pages, Math.Clamp(pages.CurrentPage, 0, pages.Pages.Count - 1));
    }

    /// <summary>
    /// Pirate: diary drawings - loads the leaf on screen after its content was replaced
    /// (a restore from the database). A doodle made while the snapshot was loading wins,
    /// exactly like typed text does.
    /// </summary>
    public void Refresh(EntityUid uid)
    {
        if (!TryComp<PaperPagesComponent>(uid, out var pages)
            || !TryComp<PaintSurfaceComponent>(uid, out var surface))
            return;

        if (surface.Image is { } live && HasDrawing(live))
            return;

        LoadLeaf((uid, surface), pages, Math.Clamp(pages.CurrentPage, 0, pages.Pages.Count - 1));
    }

    private void StoreLeaf(
        Entity<PaperComponent> paper,
        Entity<PaintSurfaceComponent> surface,
        PaperPagesComponent pages,
        int index)
    {
        if (index < 0 || index >= pages.Pages.Count)
            return;

        var encoded = surface.Comp.Image is { } image && HasDrawing(image)
            ? PaintImageCodec.Encode(image)
            : string.Empty;

        if (pages.Pages[index].Drawing == encoded)
            return;

        pages.Pages[index].Drawing = encoded;
        _paper.SyncPages(paper, pages);
    }

    private void LoadLeaf(Entity<PaintSurfaceComponent> surface, PaperPagesComponent pages, int index)
    {
        if (index < 0 || index >= pages.Pages.Count)
            return;

        var drawing = pages.Pages[index].Drawing;
        PaintImage? image = null;
        if (drawing.Length > 0)
            image = PaintImageCodec.Decode(drawing, surface.Comp.Size);

        _paint.SetImage(surface, image ?? new PaintImage(surface.Comp.Size.X, surface.Comp.Size.Y, surface.Comp.Background));
    }

    private static bool HasDrawing(PaintImage image)
    {
        foreach (var pixel in image.Pixels)
        {
            if (pixel != 0)
                return true;
        }

        return false;
    }
}
