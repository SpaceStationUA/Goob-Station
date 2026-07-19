// SPDX-FileCopyrightText: 2025 Coenx-flex
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._Pirate.CorticalBorer;

[Prototype("borerChemical")]
public sealed partial class CorticalBorerChemicalPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// Chemical point cost per unit of reagent.
    /// </summary>
    [DataField]
    public int Cost { get; set; } = 5;

    /// <summary>
    /// Reagent injected into the host.
    /// </summary>
    [DataField]
    public string Reagent { get; set; } = string.Empty;
}
