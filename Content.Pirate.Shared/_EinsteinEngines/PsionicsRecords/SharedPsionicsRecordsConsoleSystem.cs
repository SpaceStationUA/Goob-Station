using Content.Shared.Access.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.PDA;
using Content.Shared.Psionics;
using Content.Shared.Psionics.Components;

/// <summary>
/// EVERYTHING HERE IS A MODIFIED VERSION OF CRIMINAL RECORDS
/// </summary>

namespace Content.Shared.PsionicsRecords.Systems;

public abstract class SharedPsionicsRecordsConsoleSystem : EntitySystem
{
    /// <summary>
    /// Updates psionics record icons on ID cards that match the given name.
    /// If the status is None, removes the component; otherwise, sets the appropriate icon.
    /// </summary>
    public void UpdatePsionicsIdentity(string name, PsionicsStatus status)
    {
        // Find all ID cards with matching name
        var query = EntityQueryEnumerator<IdCardComponent>();

        while (query.MoveNext(out var uid, out var idCard))
        {
            if (!name.Equals(idCard.FullName))
                continue;

            if (status == PsionicsStatus.None)
                RemComp<PsionicsRecordComponent>(uid);
            else
                SetPsionicsIcon(name, status, uid);
        }
    }

    /// <summary>
    /// Decides the icon that should be displayed on the ID card based on the psionics status
    /// </summary>
    public void SetPsionicsIcon(string name, PsionicsStatus status, EntityUid idCardUid)
    {
        EnsureComp<PsionicsRecordComponent>(idCardUid, out var record);

        var previousIcon = record.StatusIcon;

        record.StatusIcon = status switch
        {
            PsionicsStatus.Suspected => "PsionicsIconSuspected",
            PsionicsStatus.Registered => "PsionicsIconRegistered",
            PsionicsStatus.Abusing => "PsionicsIconAbusing",
            _ => record.StatusIcon
        };

        if (previousIcon != record.StatusIcon)
            Dirty(idCardUid, record);
    }
}
