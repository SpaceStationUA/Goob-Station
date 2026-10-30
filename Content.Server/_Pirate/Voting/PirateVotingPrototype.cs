// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.GameTicking.Presets;
using Robust.Shared.Prototypes;

namespace Content.Server._Pirate.Voting;

[Prototype]
public sealed partial class PirateVotingPrototype : IPrototype
{
    public const string DefaultId = "Default";

    [IdDataField]
    public string ID { get; private set; } = string.Empty;

    [DataField]
    public int FinalistCount = 3;

    // Fractions to subtract, from the most recent valid round to the oldest.
    [DataField]
    public Dictionary<ProtoId<GamePresetPrototype>, List<float>> ModePenalties = new();
}
