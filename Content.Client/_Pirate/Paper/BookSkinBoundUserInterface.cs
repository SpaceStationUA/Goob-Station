// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Content.Shared._Pirate.Paper;
using Content.Shared.Tag;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Paper;

/// <summary>
/// Pirate: opens the cover picker for a book and reports the picked cover back to the server.
/// </summary>
[UsedImplicitly]
public sealed class BookSkinBoundUserInterface : BoundUserInterface
{
    private BookSkinWindow? _window;

    public BookSkinBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BookSkinWindow>();
        _window.OnCoverSelected += skin => SendMessage(new BookSkinSelectedMessage(skin));

        // The component is networked, so the window can fill itself right away; the server
        // state arriving right after only refreshes the highlight.
        if (EntMan.TryGetComponent<BookSkinComponent>(Owner, out var skinComp))
            _window.Populate(skinComp.Skin?.Id, skinComp.SourceTag);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not BookSkinState cast || _window == null)
            return;

        ProtoId<TagPrototype> sourceTag = "Book";
        if (EntMan.TryGetComponent<BookSkinComponent>(Owner, out var skinComp))
            sourceTag = skinComp.SourceTag;

        _window.Populate(cast.Skin?.Id, sourceTag);
    }
}
