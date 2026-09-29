// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.Emp;
using Content.Shared.Inventory.Events;
using Robust.Shared.GameObjects;

namespace Content.Pirate.Server.Emp;

/// <summary>Tracks shielded wearers; the modular suit system handles the shared EMP event.</summary>
public sealed class EmpShieldClothingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EmpShieldClothingComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<EmpShieldClothingComponent, GotUnequippedEvent>(OnUnequipped);
    }

    private void OnEquipped(Entity<EmpShieldClothingComponent> ent, ref GotEquippedEvent args)
    {
        if ((args.SlotFlags & ent.Comp.Slots) == 0)
            return;

        EnsureComp<EmpShieldedWearerComponent>(args.Equipee).Sources.Add(ent);
    }

    private void OnUnequipped(Entity<EmpShieldClothingComponent> ent, ref GotUnequippedEvent args)
    {
        if (!TryComp<EmpShieldedWearerComponent>(args.Equipee, out var shielded))
            return;

        shielded.Sources.Remove(ent);
        if (shielded.Sources.Count == 0)
            RemComp<EmpShieldedWearerComponent>(args.Equipee);
    }

    /// <summary>
    /// True if <paramref name="target"/> is, or is contained (at any depth) inside, a shielded wearer.
    /// </summary>
    public bool IsShielded(Entity<MetaDataComponent> target)
    {
        var current = target.Owner;
        var metadata = target.Comp;

        while (true)
        {
            if (HasComp<EmpShieldedWearerComponent>(current))
                return true;

            // Follow containment only; do not cross map or grid parents.
            if ((metadata.Flags & MetaDataFlags.InContainer) == 0 ||
                !TryComp(current, out TransformComponent? transform) ||
                !transform.ParentUid.Valid ||
                !TryComp(transform.ParentUid, out MetaDataComponent? parentMetadata))
            {
                return false;
            }

            current = transform.ParentUid;
            metadata = parentMetadata;
        }
    }
}
