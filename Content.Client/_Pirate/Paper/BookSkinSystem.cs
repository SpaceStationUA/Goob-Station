// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.Paper;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Paper;

/// <summary>
/// Pirate: applies the cover chosen through <see cref="BookSkinComponent"/>.
/// Only the states and colours of the book's own layers are swapped for the ones the
/// source book prototype uses - the sprite component itself is left alone. Replacing it
/// wholesale would also reset things like container occlusion, which paints the book
/// over whoever is carrying it.
/// </summary>
public sealed class BookSkinSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    /// <summary>The item's own look, captured before the first cover was applied.</summary>
    private readonly Dictionary<EntityUid, LayerLook[]> _originalLayers = new();

    private readonly record struct LayerLook(string State, Color Color);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BookSkinComponent, AfterAutoHandleStateEvent>(OnSkinStateChanged);
        SubscribeLocalEvent<BookSkinComponent, ComponentShutdown>(OnSkinShutdown);
    }

    private void OnSkinStateChanged(Entity<BookSkinComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ApplySkin(ent);
    }

    private void OnSkinShutdown(Entity<BookSkinComponent> ent, ref ComponentShutdown args)
    {
        _originalLayers.Remove(ent.Owner);
    }

    private void ApplySkin(Entity<BookSkinComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent.Owner, out var sprite))
            return;

        var original = CaptureOriginal(ent.Owner, sprite);
        var layerCount = sprite.AllLayers.Count();

        // No cover chosen: put the item's own look back.
        if (ent.Comp.Skin is not { } skinId)
        {
            for (var i = 0; i < original.Length && i < layerCount; i++)
                SetLayer(ent, sprite, i, original[i]);
            return;
        }

        if (!_proto.TryIndex(skinId.Id, out EntityPrototype? sourceProto))
            return;

        if (!sourceProto.TryGetComponent(out SpriteComponent? source, EntityManager.ComponentFactory))
            return;

        // What the source book has to offer, sorted by the role its state name gives away.
        var wanted = new Dictionary<string, LayerLook>();
        foreach (var layer in source.AllLayers)
        {
            var state = layer.RsiState.Name;
            var role = RoleOf(state);
            if (role == null || state == null)
                continue;

            // Last one wins, so a book with a glow under its glyph yields the glyph.
            wanted[role] = new LayerLook(state, layer.Color);
        }

        for (var i = 0; i < layerCount; i++)
        {
            var role = RoleOf(sprite[i].RsiState.Name);
            if (role == null)
                continue;

            // A cover without this kind of layer falls back to the item's own look.
            if (!wanted.TryGetValue(role, out var look))
            {
                if (i >= original.Length)
                    continue;

                look = original[i];
            }

            SetLayer(ent, sprite, i, look);
        }
    }

    private void SetLayer(Entity<BookSkinComponent> ent, SpriteComponent sprite, int index, LayerLook look)
    {
        // Same RSI only: the book covers all live in books.rsi, and an unknown state
        // would just spam errors instead of changing anything.
        if (sprite[index].ActualRsi is not { } rsi || !rsi.TryGetState(look.State, out _))
            return;

        _sprite.LayerSetRsiState((ent.Owner, sprite), index, look.State);
        _sprite.LayerSetColor((ent.Owner, sprite), index, look.Color);
    }

    private LayerLook[] CaptureOriginal(EntityUid uid, SpriteComponent sprite)
    {
        if (_originalLayers.TryGetValue(uid, out var cached))
            return cached;

        var layerCount = sprite.AllLayers.Count();
        var original = new LayerLook[layerCount];
        for (var i = 0; i < layerCount; i++)
            original[i] = new LayerLook(sprite[i].RsiState.Name ?? string.Empty, sprite[i].Color);

        _originalLayers[uid] = original;
        return original;
    }

    /// <summary>
    /// Pirate: the exact layers <see cref="ApplySkin"/> would draw for this book wearing the
    /// given cover - the book's own layers with each swappable role replaced by the cover's
    /// state, tinted with the layer colour, resolved against the book's RSI. The cover picker
    /// needs this because the source book's raw sprite is not what ends up on the book:
    /// it has extra layers and no tints.
    /// Null when the book has no previewable sprite.
    /// </summary>
    public List<(Texture Texture, Color Color)>? GetCoverPreview(EntityUid book, EntProtoId cover)
    {
        // The live sprite only supplies the RSI: the states must be resolved against the
        // same RSI SetLayer() validates against, i.e. the book's own (books.rsi).
        if (!TryComp<SpriteComponent>(book, out var live) || live.BaseRSI is not { } rsi)
            return null;

        var bookProto = MetaData(book).EntityPrototype;
        if (bookProto == null ||
            !bookProto.TryGetComponent(out SpriteComponent? baseLayers, EntityManager.ComponentFactory) ||
            !_proto.TryIndex(cover.Id, out EntityPrototype? sourceProto) ||
            !sourceProto.TryGetComponent(out SpriteComponent? source, EntityManager.ComponentFactory))
            return null;

        // Same "last one wins" mapping ApplySkin() uses.
        var wanted = new Dictionary<string, LayerLook>();
        foreach (var layer in source.AllLayers)
        {
            var state = layer.RsiState.Name;
            var role = RoleOf(state);
            if (role == null || state == null)
                continue;

            wanted[role] = new LayerLook(state, layer.Color);
        }

        var preview = new List<(Texture, Color)>();
        foreach (var layer in baseLayers.AllLayers)
        {
            var state = layer.RsiState.Name ?? string.Empty;
            var look = new LayerLook(state, layer.Color);

            var role = RoleOf(state);
            if (role != null && wanted.TryGetValue(role, out var swap) && rsi.TryGetState(swap.State, out _))
                look = swap;

            // A state missing from the book's RSI is skipped by SetLayer() in game too.
            if (!rsi.TryGetState(look.State, out var tex))
                continue;

            preview.Add((((IDirectionalTextureProvider)tex).Default, look.Color));
        }

        return preview.Count > 0 ? preview : null;
    }

    /// <summary>
    /// Which part of a book a sprite state belongs to. Books only ever differ in their
    /// cover, decor, icon and overlay, so those can be swapped one by one.
    /// Public so the cover picker can tell a transplantable book from one whose sprite
    /// shares none of these states (skillbooks, schematics, mime guides).
    /// </summary>
    public static string? RoleOf(string? state)
    {
        if (string.IsNullOrEmpty(state))
            return null;

        if (state == "paper")
            return "paper";
        if (state.StartsWith("cover_", StringComparison.Ordinal))
            return "cover";
        if (state.StartsWith("decor_", StringComparison.Ordinal))
            return "decor";
        if (state.StartsWith("icon_", StringComparison.Ordinal))
            return "icon";
        if (state.StartsWith("overlay_", StringComparison.Ordinal))
            return "overlay";

        return null;
    }
}
