// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared._Pirate.StationRecords;
using Content.Shared.Access;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server._Pirate.StationRecords;

[RegisterComponent, Access(typeof(RecordsCartridgeSystem))]
public sealed partial class RecordsCartridgeComponent : Component
{
    [DataField]
    public RecordsCartridgeTab Tab = RecordsCartridgeTab.Security;

    // A missing tab grants access; each set requires all of its tags.
    [DataField]
    public Dictionary<RecordsCartridgeTab, List<HashSet<ProtoId<AccessLevelPrototype>>>> Access = new();
}
