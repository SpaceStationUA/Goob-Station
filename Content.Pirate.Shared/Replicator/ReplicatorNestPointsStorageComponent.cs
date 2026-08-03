// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Pirate.Shared.Replicator;

[RegisterComponent, NetworkedComponent]
public sealed partial class ReplicatorNestPointsStorageComponent : Component
{
    public int TotalPoints;

    public int TotalReplicators;

    public int Level;
}
