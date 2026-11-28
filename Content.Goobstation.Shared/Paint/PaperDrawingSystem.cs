using Content.Shared._Pirate.Crayon;
using Content.Shared._Pirate.Paper;
using Content.Shared._Pirate.PersistentText; // Pirate: diary ownership
using Content.Shared.Crayon;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;

namespace Content.Goobstation.Shared.Paint;

/// <summary>
/// Pirate: chalk drawing on paper. Right-clicking a sheet with a crayon opens the same
/// painting UI canvases use, painting in the crayon's colour; a crayon with a selectable
/// colour (rainbow/artist/plasma) may pick any RGB in that window.
/// </summary>
public sealed partial class PaperDrawingSystem : EntitySystem
{
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private PaperSystem _paper = default!; // Pirate: diary ownership

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PaperComponent, PaperDrawingAttemptEvent>(OnPaperDrawingAttempt);
    }

    private void OnPaperDrawingAttempt(Entity<PaperComponent> ent, ref PaperDrawingAttemptEvent args)
    {
        if (args.Handled
            || !HasComp<PaintSurfaceComponent>(ent)
            || !HasComp<PaintToolComponent>(args.Used))
            return;

        // Pirate: a stamped or tamper-proofed document is final, exactly like its text.
        if (ent.Comp.StampedBy.Count > 0 || ent.Comp.EditingDisabled)
        {
            _popup.PopupClient(Loc.GetString("paper-tamper-proof-modified-message"), ent.Owner, args.User);
            args.Handled = true;
            return;
        }

        // Pirate: diary ownership - only the diary's bound owner may draw in it, exactly
        // like only they may write in it.
        if (TryComp<PersistentTextComponent>(ent, out var persistentText)
            && !CanDraw(ent.Owner, persistentText, args.User))
        {
            _popup.PopupClient(Loc.GetString("persistent-text-cant-write"), ent.Owner, args.User);
            args.Handled = true;
            return;
        }

        if (!_ui.HasUi(ent.Owner, PaintUiKey.Key))
            return;

        // Pirate: crayon gates (novice protection, ...) apply to the drawing session too.
        if (HasComp<CrayonComponent>(args.Used))
        {
            var attempt = new CrayonDrawAttemptEvent(args.User);
            RaiseLocalEvent(args.Used, ref attempt);
            if (attempt.Cancelled)
            {
                args.Handled = true;
                return;
            }
        }

        _ui.OpenUi(ent.Owner, PaintUiKey.Key, args.User);
        args.Handled = true;
    }

    /// <summary>
    /// Pirate: diary ownership - whether <paramref name="user"/> may draw into the bound diary.
    /// An unbound persistent document still belongs to nobody, so anyone may draw in it.
    /// </summary>
    /// <remarks>
    /// The account behind the character is only known to the server, so predicted clients
    /// decide by the bound character name; the server verifies the account here and again
    /// for every stroke (see PaintSurfaceSystem).
    /// </remarks>
    private bool CanDraw(EntityUid uid, PersistentTextComponent persistentText, EntityUid user)
    {
        return _paper.CanWritePredicted(uid, persistentText, user);
    }
}
