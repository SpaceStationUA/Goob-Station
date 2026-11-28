// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Store;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Store;

/// <summary>Optional stock for a specific set of stores. Its entries are local to this catalog.</summary>
[Prototype("storeCatalog")]
public sealed partial class StoreCatalogPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
    [DataField(required: true)] public List<ListingPrototype> Listings = new();
}
