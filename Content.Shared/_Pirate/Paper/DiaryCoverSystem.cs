// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Goobstation.Common.Paper;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._Pirate.Paper;

// Shared prediction prevents the UI from opening before the server rejects a closed diary.
public sealed class DiaryCoverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DiaryCoverComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<DiaryCoverComponent, PaperContentChangedEvent>(OnContentChanged);
        SubscribeLocalEvent<DiaryCoverComponent, UseInHandEvent>(OnUseInHand,
            before: new[] { typeof(PaperSystem), typeof(ActivatableUISystem) });
        SubscribeLocalEvent<DiaryCoverComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(PaperSystem) });
        SubscribeLocalEvent<DiaryCoverComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<DiaryCoverComponent, BeingSignedAttemptEvent>(OnBeingSignedAttempt);
        SubscribeLocalEvent<DiaryCoverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnMapInit(Entity<DiaryCoverComponent> ent, ref MapInitEvent args)
    {
        UpdatePages(ent);
    }

    private void OnContentChanged(Entity<DiaryCoverComponent> ent, ref PaperContentChangedEvent args)
    {
        UpdatePages(ent);
    }

    private void OnUseInHand(Entity<DiaryCoverComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Open)
            return;

        SetOpen(ent, true, args.User);
        args.Handled = true;
    }

    private void OnInteractUsing(Entity<DiaryCoverComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.Open)
            return;

        PopupClosed(ent, args.User);
        args.Handled = true;
    }

    private void OnUiOpenAttempt(Entity<DiaryCoverComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (ent.Comp.Open)
            return;

        if (!args.Silent)
            PopupClosed(ent, args.User);

        args.Cancel();
    }

    private void OnBeingSignedAttempt(Entity<DiaryCoverComponent> ent, ref BeingSignedAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Open)
            return;

        PopupClosed(ent, args.Signer);
        args.Cancelled = true;
    }

    private void OnGetAltVerbs(Entity<DiaryCoverComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var open = !ent.Comp.Open;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(open ? "diary-cover-verb-open" : "diary-cover-verb-close"),
            Act = () => SetOpen(ent, open, user),
            Priority = 5,
        });
    }

    public void SetOpen(Entity<DiaryCoverComponent> ent, bool open, EntityUid? user = null)
    {
        if (ent.Comp.Open == open)
            return;

        ent.Comp.Open = open;
        Dirty(ent);
        RaiseChanged(ent);

        _audio.PlayPredicted(open ? ent.Comp.OpenSound : ent.Comp.CloseSound, ent, user);

        if (!open)
            _ui.CloseUi(ent.Owner, PaperComponent.PaperUiKey.Key);
    }

    private void UpdatePages(Entity<DiaryCoverComponent> ent)
    {
        if (!TryComp<PaperComponent>(ent, out var paper))
            return;

        var leaves = PaperPageFormat.Decode(paper.Content);
        var left = HasWriting(leaves, 0);
        var right = HasWriting(leaves, 1);
        if (ent.Comp.PageLeft == left && ent.Comp.PageRight == right)
            return;

        ent.Comp.PageLeft = left;
        ent.Comp.PageRight = right;
        Dirty(ent);
        RaiseChanged(ent);
    }

    private void RaiseChanged(EntityUid uid)
    {
        var ev = new DiaryCoverChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    private static bool HasWriting(List<PaperPage> leaves, int index)
    {
        if (index >= leaves.Count)
            return false;

        var leaf = leaves[index];
        return !string.IsNullOrWhiteSpace(leaf.Content) || leaf.Signatures.Count > 0;
    }

    private void PopupClosed(Entity<DiaryCoverComponent> ent, EntityUid user)
    {
        _popup.PopupClient(Loc.GetString("diary-cover-closed", ("target", ent.Owner)), ent, user);
    }
}

[ByRefEvent]
public record struct PaperContentChangedEvent;
