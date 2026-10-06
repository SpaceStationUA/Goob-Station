// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Paper;

/// <summary>
/// Pirate: chameleon-style covers for diaries and books - the item can be reskinned with
/// the look of any ordinary book.
/// Only the choice is replicated; the sprite itself is copied client-side, because
/// <c>SpriteComponent</c> only exists in the client assembly (see the client BookSkinSystem).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class BookSkinComponent : Component
{
    /// <summary>
    /// Prototype whose sprite this book currently copies. Null keeps the item's own look.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? Skin;

    /// <summary>
    /// Only prototypes carrying this tag may be picked as a look - ordinary books.
    /// </summary>
    [DataField]
    public ProtoId<TagPrototype> SourceTag = "Book";
}

/// <summary>
/// Pirate: cover picker window, in the style of the agent ID card menu.
/// </summary>
[Serializable, NetSerializable]
public enum BookSkinUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class BookSkinState(EntProtoId? skin) : BoundUserInterfaceState
{
    public EntProtoId? Skin { get; } = skin;
}

[Serializable, NetSerializable]
public sealed class BookSkinSelectedMessage(EntProtoId skin) : BoundUserInterfaceMessage
{
    public EntProtoId Skin { get; } = skin;
}

/// <summary>
/// Pirate: book prototypes whose sprite may be copied as a cover. Lives in shared code so
/// the client window offers exactly the set the server accepts on selection.
/// </summary>
public static class BookSkinLooks
{
    public static List<EntityPrototype> Get(
        IPrototypeManager proto,
        IComponentFactory factory,
        TagSystem tag,
        ProtoId<TagPrototype> sourceTag)
    {
        var looks = new List<EntityPrototype>();

        foreach (var prototype in proto.EnumeratePrototypes<EntityPrototype>())
        {
            if (prototype.Abstract)
                continue;

            // Loadout copies wear the diary's own look anyway and would only clutter the list.
            if (prototype.SetSuffix != null &&
                prototype.SetSuffix.Contains("loadout", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!prototype.TryGetComponent(out TagComponent? tags, factory))
                continue;

            if (!tag.HasTag(tags, sourceTag))
                continue;

            looks.Add(prototype);
        }

        looks.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return looks;
    }
}
