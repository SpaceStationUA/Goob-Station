// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;

namespace Content.Goobstation.Shared.Autosurgeon;

public sealed partial class AutoSurgeonSystem
{
    private static string? GetReplacementOrgan(AutoSurgeonComponent autosurgeon, BodyPartComponent part)
    {
        foreach (var (slot, prototype) in autosurgeon.AlternateOrgans)
        {
            if (part.Organs.ContainsKey(slot))
                return prototype.Id;
        }

        return autosurgeon.NewPartProto;
    }

    private bool TryReplaceOrgan(EntityUid part, EntityUid replacement)
    {
        if (!TryComp<OrganComponent>(replacement, out var organ) || !_body.CanInsertOrgan(part, organ.SlotId))
            return false;

        var previous = _body.GetPartOrgans(part).FirstOrDefault(organInPart => organInPart.Component.SlotId == organ.SlotId).Id;
        if (previous.Valid && !_body.RemoveOrgan(previous))
            return false;

        if (_body.InsertOrgan(part, replacement, organ.SlotId))
            return true;

        if (previous.Valid)
            _body.InsertOrgan(part, previous, organ.SlotId);

        return false;
    }
}
