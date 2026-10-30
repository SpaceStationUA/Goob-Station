// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Triage.MedicalRecords;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class MedicalRecordComponent : Component
{
    [DataField, AutoNetworkedField]
    public MedicalRecord Record = new();
}
