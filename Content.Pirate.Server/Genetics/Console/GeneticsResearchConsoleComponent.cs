// SPDX-License-Identifier: AGPL-3.0-or-later

// Pirate: explicit imports supplied by Trauma global usings.
using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Shared.Radio;

namespace Content.Pirate.Server.Genetics.Console;

/// <summary>
/// For a genetics console with <c>ResearchClientComponent</c>, when you sequence a new mutation it gives points and announces it to sci radio.
/// Does nothing for multiple sequencings of the same mutation in this round.
/// </summary>
[RegisterComponent, Access(typeof(GeneticsResearchConsoleSystem))]
public sealed partial class GeneticsResearchConsoleComponent : Component
{
    /// <summary>
    /// Research points given, scaled by its difficulty.
    /// </summary>
    [DataField]
    public int PointsPerDifficulty = 500;

    /// <summary>
    /// Channel to send the points message on.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "Science";
}
