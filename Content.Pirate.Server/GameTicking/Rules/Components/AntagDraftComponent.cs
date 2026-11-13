// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(AntagDraftSystem))]
public sealed partial class AntagDraftComponent : Component
{
    [DataField]
    public float PlayersPerAntagMin = 10f;

    [DataField]
    public float PlayersPerAntagMax = 12f;

    [DataField]
    public int MinAntags = 1;

    [DataField]
    public int MaxAntags;

    [DataField(required: true)]
    public List<AntagDraftEntry> FirstPick = new();

    [DataField(required: true)]
    public List<AntagDraftEntry> Picks = new();
}

[DataDefinition]
public sealed partial class AntagDraftEntry
{
    [DataField(required: true)]
    public EntProtoId Rule;

    [DataField]
    public float Weight = 1f;

    // The higher of this and the rule's minPlayers applies.
    [DataField]
    public int MinPlayers;

    [DataField]
    public int Antags = 1;

    // A repeated pick adds to the same rule.
    [DataField]
    public int MaxPicks = 1;

    // A major ends the draft and is valid only in FirstPick.
    [DataField]
    public bool Major;
}
