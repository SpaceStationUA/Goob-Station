// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Roles;

[Prototype]
public sealed partial class SpeciesJobWhitelistPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<SpeciesPrototype> Species;

    [DataField(required: true)]
    public HashSet<ProtoId<JobPrototype>> Jobs = new();
}
