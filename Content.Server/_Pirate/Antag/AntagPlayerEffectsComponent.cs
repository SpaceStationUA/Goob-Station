// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.EntityEffects;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Antag;

[RegisterComponent, Access(typeof(AntagPlayerEffectsSystem))]
public sealed partial class AntagPlayerEffectsComponent : Component
{
    [DataField]
    public List<ProtoId<EntityEffectPrototype>> Packages = new();

    [DataField]
    public EntityEffect[]? Effects;
}
