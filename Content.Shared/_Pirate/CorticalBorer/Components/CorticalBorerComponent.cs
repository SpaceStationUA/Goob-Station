// SPDX-FileCopyrightText: 2025 Coenx-flex
// SPDX-FileCopyrightText: 2025 Cojoke
// SPDX-FileCopyrightText: 2025 Ilya246
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Starlight.CollectiveMind;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.CorticalBorer;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CorticalBorerComponent : Component
{
    /// <summary>
    /// The borer's current host.
    /// </summary>
    [ViewVariables]
    public EntityUid? Host;

    /// <summary>
    /// Chemical points used for abilities and reagent injection.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField, DataField]
    public int ChemicalPoints = 50;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int ChemicalGenerationRate = 1;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int ChemicalPointCap = 250;

    public int InjectAmount = 10;

    public int UiUpdateInterval = 5;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan ControlDuration = TimeSpan.FromSeconds(40);

    public TimeSpan UpdateTimer = TimeSpan.Zero;
    public float UpdateCooldown = 1f;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool CanReproduce = true;

    [ViewVariables]
    public bool HasLaidEgg;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public EntProtoId EggProto = "CorticalBorerEgg";

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public int EggCost = 200;

    [DataField]
    public bool ControlingHost;

    [DataField]
    public ComponentRegistry? AddOnInfest;

    [DataField]
    public ComponentRegistry? RemoveOnInfest;

    [DataField]
    public ProtoId<AlertPrototype> ChemicalAlert = "Chemicals";

    [DataField]
    public ProtoId<CollectiveMindPrototype> HivemindChannel = "CorticalBorer";

    public readonly List<EntProtoId> InitialCorticalBorerActions = new()
    {
        "ActionCorticalBorerInfest",
        "ActionCorticalBorerEject",
        "ActionCorticalBorerChemMenu",
        "ActionCheckBlood",
        "ActionControlHost",
    };
}
