using Content.Shared._Pirate.Xelthia;
using Content.Shared.Forensics.Components;
using Content.Shared.Inventory.VirtualItem;

namespace Content.Server.Forensics;

public sealed partial class ForensicsSystem
{
    private bool TryApplyXelthiaEvidence(EntityUid user, EntityUid target, ForensicsComponent evidence)
    {
        var xelthia = HasComp<XelthiaComponent>(user);
        var hasGloves = _inventory.TryGetSlotEntity(user, "gloves", out var gloves);
        if (_inventory.TryGetSlotEntity(user, "outerClothing", out var jacket)
            && (!hasGloves || HasComp<VirtualItemComponent>(gloves))
            && (xelthia || HasComp<GloveJacketComponent>(jacket)))
        {
            if (TryComp<FiberComponent>(jacket, out var fiber) && !string.IsNullOrEmpty(fiber.FiberMaterial))
            {
                evidence.Fibers.Add(string.IsNullOrEmpty(fiber.FiberColor)
                    ? Loc.GetString("forensic-fibers", ("material", fiber.FiberMaterial))
                    : Loc.GetString("forensic-fibers-colored", ("color", fiber.FiberColor), ("material", fiber.FiberMaterial)));
            }

            if (HasComp<FingerprintMaskComponent>(jacket))
            {
                Dirty(target, evidence);
                return true;
            }
        }

        if (!xelthia)
            return false;

        // Omu Xelthia leave sticky residue rather than human fingerprints.
        evidence.Residues.Add(Loc.GetString("forensic-residue", ("adjective", "residue-sticky")));
        Dirty(target, evidence);
        return true;
    }
}
