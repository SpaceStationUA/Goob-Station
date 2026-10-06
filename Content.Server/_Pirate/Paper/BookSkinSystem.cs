// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using Content.Shared._Pirate.Paper;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Paper;

/// <summary>
/// Pirate: chameleon-style covers - right-click a diary/book to open the cover picker
/// (an agent-card style window) listing the looks of ordinary books. This system serves
/// the menu and remembers the choice; the sprite itself is copied on the client, where
/// SpriteComponent lives. The choice survives round changes by riding in the persisted
/// text of a persistent diary as a hidden meta block (see <see cref="AttachSkinMeta"/>).
/// </summary>
public sealed class BookSkinSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    /// <summary>File separator, framing the meta block in front of a persisted document.</summary>
    private const char MetaMarker = '\u001C';

    /// <summary>Unit separator, splitting a meta key from its value.</summary>
    private const char MetaKeyValue = '\u001F';

    private const string SkinMetaKey = "skin";

    private readonly Dictionary<string, List<EntityPrototype>> _looksCache = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookSkinComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<BookSkinComponent, BookSkinSelectedMessage>(OnCoverSelected);
    }

    private void OnGetAltVerbs(Entity<BookSkinComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (GetLooks(ent.Comp).Count == 0)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("book-skin-verb"),
            Icon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Objects/Misc/books.rsi"), "icon_text"),
            Act = () => OpenSkinMenu(ent, user),
            Priority = 1,
        });
    }

    private void OpenSkinMenu(Entity<BookSkinComponent> ent, EntityUid user)
    {
        if (GetLooks(ent.Comp).Count == 0)
            return;

        _ui.SetUiState(ent.Owner, BookSkinUiKey.Key, new BookSkinState(ent.Comp.Skin));
        _ui.OpenUi(ent.Owner, BookSkinUiKey.Key, user);
    }

    private void OnCoverSelected(Entity<BookSkinComponent> ent, ref BookSkinSelectedMessage args)
    {
        if (!IsAvailable(ent.Comp, args.Skin))
            return;

        ent.Comp.Skin = args.Skin;
        Dirty(ent);

        // Keep the window open so covers can be tried one after another; the new state
        // moves the highlight to the freshly picked cover.
        _ui.SetUiState(ent.Owner, BookSkinUiKey.Key, new BookSkinState(ent.Comp.Skin));
        _popup.PopupClient(Loc.GetString("book-skin-changed"), ent.Owner, args.Actor);
    }

    private bool IsAvailable(BookSkinComponent comp, EntProtoId look)
    {
        foreach (var proto in GetLooks(comp))
        {
            if (string.Equals(proto.ID, look.Id, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Prototypes whose sprite may be copied: everything tagged as a book, minus abstract
    /// prototypes and loadout copies. Cached, since prototypes never change at runtime and
    /// this is asked for on every verb request.
    /// </summary>
    private List<EntityPrototype> GetLooks(BookSkinComponent comp)
    {
        var cacheKey = comp.SourceTag.Id;
        if (_looksCache.TryGetValue(cacheKey, out var cached))
            return cached;

        var looks = BookSkinLooks.Get(_proto, EntityManager.ComponentFactory, _tag, comp.SourceTag);
        _looksCache[cacheKey] = looks;
        return looks;
    }

    /// <summary>
    /// Pirate: the cover survives between rounds by riding in front of the persisted text
    /// of a persistent diary - no schema change, and the block is stripped on restore so
    /// nobody ever sees it.
    /// </summary>
    public string AttachSkinMeta(EntityUid uid, string content)
    {
        if (!TryComp<BookSkinComponent>(uid, out var skin))
            return content;

        var current = skin.Skin;
        if (current == null)
            return content;

        return $"{MetaMarker}{SkinMetaKey}{MetaKeyValue}{current.Value.Id}{MetaMarker}{content}";
    }

    /// <summary>
    /// Pirate: reads the meta block written by <see cref="AttachSkinMeta"/>, applies it to
    /// the item and returns the document itself.
    /// </summary>
    public string ExtractSkinMeta(EntityUid uid, string content)
    {
        if (content.Length < 2 || content[0] != MetaMarker)
            return content;

        var end = content.IndexOf(MetaMarker, 1);
        if (end < 0)
            return content;

        var meta = content[1..end];
        var separator = meta.IndexOf(MetaKeyValue);

        // Only trust a cover that actually exists for this item: the id comes from the
        // database and may name a prototype removed between versions - an invalid one
        // would be replicated and then saved back by AttachSkinMeta.
        if (separator > 0 &&
            meta[..separator] == SkinMetaKey &&
            !string.IsNullOrWhiteSpace(meta[(separator + 1)..]) &&
            TryComp<BookSkinComponent>(uid, out var skin) &&
            IsAvailable(skin, meta[(separator + 1)..]))
        {
            skin.Skin = meta[(separator + 1)..];
            Dirty(uid, skin);
        }

        return content[(end + 1)..];
    }
}
