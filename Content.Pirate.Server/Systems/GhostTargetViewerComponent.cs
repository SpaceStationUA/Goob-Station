// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.Systems;

[RegisterComponent]
public sealed partial class GhostTargetViewerComponent : Component
{
    public readonly HashSet<EntityUid> Spirits = new();
    public bool HadLayer;
}
