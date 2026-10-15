// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Contractors.Prototypes;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.StationRecords;

namespace Content.Server.StationRecords.Systems;

public sealed partial class StationRecordsSystem
{
    private bool MatchesAnyField(GeneralStationRecord record, string lowerCaseFilter)
    {
        return Matches(record.Name)
            || Matches(record.JobTitle)
            || Matches(record.Species)
            || Matches(_prototypeManager.TryIndex<SpeciesPrototype>(record.Species, out var species) ? Loc.GetString(species.Name) : null)
            || Matches(record.Nationality)
            || Matches(_prototypeManager.TryIndex<NationalityPrototype>(record.Nationality, out var nationality) ? Loc.GetString(nationality.NameKey) : null)
            || Matches(record.Employer)
            || Matches(_prototypeManager.TryIndex<EmployerPrototype>(record.Employer, out var employer) ? Loc.GetString(employer.NameKey) : null)
            || Matches(record.Fingerprint)
            || Matches(record.DNA);

        bool Matches(string? value)
        {
            return !string.IsNullOrEmpty(value) && value.ToLower().Contains(lowerCaseFilter);
        }
    }
}
