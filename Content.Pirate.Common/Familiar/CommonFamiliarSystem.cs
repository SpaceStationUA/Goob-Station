// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Common.Familiar;

/// <summary>
/// Shared API for marking an entity as a familiar of another entity.
/// </summary>
public abstract class CommonFamiliarSystem : EntitySystem
{
    public abstract void SetMaster(EntityUid uid, EntityUid master);
}
