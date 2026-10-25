// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Tag;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.Storage;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class SwordSheathComponent : Component
{
    [DataField(required: true)]
    public ProtoId<TagPrototype> SwordTag;

    // Keep server insertion order for client quick-equip prediction.
    [DataField, AutoNetworkedField]
    public List<EntityUid> RetrievalOrder = new();
}
