// SPDX-FileCopyrightText: 2025 beck <163376292+widgetbeck@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Pirate.Shared.Replicator;

[NetworkedComponent]
public abstract partial class SharedReplicatorSignComponent : Component
{
    [DataField(required: true)]
    public ResPath SpritePath = new("_Pirate/Mobs/Replicator/replicator_sign.rsi");
}
