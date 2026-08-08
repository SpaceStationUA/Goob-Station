// SPDX-FileCopyrightText: 2025 Ark
// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2025 Ilya246
// SPDX-FileCopyrightText: 2025 ark1368
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Starlight.CollectiveMind;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.CorticalBorer;

[RegisterComponent, NetworkedComponent]
public sealed partial class CorticalBorerInfestedComponent : Component
{
    [ViewVariables]
    public Entity<CorticalBorerComponent> Borer;

    public Container InfestationContainer = default!;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? ControlTimeEnd;

    [ViewVariables]
    public EntityUid? OriginalMindId;

    [ViewVariables]
    public EntityUid BorerMindId;

    public Container ControlContainer = default!;

    public List<EntityUid> RemoveAbilities = new();

    public EntityUid? LayEggAction;

    public EntityUid? RemovedReformAction;

    [ViewVariables]
    public bool HadHivemind;

    [ViewVariables]
    public ProtoId<CollectiveMindPrototype>? OldDefault;

    [ViewVariables]
    public bool AddedControlThermalVision;

    [ViewVariables]
    public bool AddedBorerNightVision;

    [ViewVariables]
    public bool? PreviousHostNightVisionActive;

    [ViewVariables]
    public bool AddedBorerThermalVision;

    [ViewVariables]
    public bool? PreviousHostThermalVisionActive;

    [ViewVariables]
    public bool IsPolymorphing;
}

[RegisterComponent, NetworkedComponent]
public sealed partial class SurgeryCorticalBorerConditionComponent : Component;
