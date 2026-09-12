// SPDX-License-Identifier: MIT

using Robust.Shared.GameObjects;

namespace Content.Shared.Materials;

/// <summary>
/// Raised before a material reclaimer consumes an entity so specialized machines can intercept it.
/// </summary>
public sealed partial class MaterialReclaimerProcessEntityEvent : EntityEventArgs
{
    public EntityUid Entity { get; }
    public bool Handled { get; set; }

    public MaterialReclaimerProcessEntityEvent(EntityUid entity)
    {
        Entity = entity;
    }
}
