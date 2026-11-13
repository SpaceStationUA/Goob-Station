// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.Station.Components;
using Content.Shared._Pirate;
using Content.Shared.Fax.Components;
using Content.Shared.Paper;
using Content.Shared.Station.Components;
using Robust.Shared.Utility;

namespace Content.Server._Pirate.Revolution;

public sealed partial class PirateRevolutionOrdersSystem
{
    private const RegexOptions FaxNameOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;

    // Map fax names vary, so matching includes English and Ukrainian patterns.
    private static readonly Regex CommandFaxes = new(
        @"captain|капітан|bridge|місток|\bhos+\b|head of security|голова безпеки|голова сб|security|безпек|\bсб\b|\bsec\b"
        + @"|warden|наглядач|brig|бриг|secpost|\bperma\b|prison|тюрм|detective|\bdets?\b|детектив"
        + @"|\bhops?\b|head of person|голова персоналу",
        FaxNameOptions);

    private static readonly Dictionary<string, Regex> DepartmentFaxes = new()
    {
        ["Cargo"] = new(@"cargo|карго|quartermaster|квартирмейстер|\bqms?\b|supply|\bsup\b|постач|mail\s*room|mail office|пошт|salvage|утиліз",
            FaxNameOptions),
        ["Engineering"] = new(@"engineer|engie|\beng\b|інженер|\bінж\b|\bгі\b|\bces?\b|atmos|атмос|supermatter|суперматер|\bteg\b",
            FaxNameOptions),
        ["Medical"] = new(@"medical|medbay|\bmed\b|медич|\bмед\b|медбей|^\s*гл\s*$|\bcmos?\b|chief medical|лікар|psych|психол|chem|хімі"
            + @"|virolog|вірусол|morgue|морг", FaxNameOptions),
        ["Science"] = new(@"scien|research|\bsci\b|\brnd\b|\brds?\b|науков|\bнв\b|xenobio|ксенобіо|robotic|робот", FaxNameOptions),
        ["Service"] = new(@"service|обслуг|\bсер\b|\bbar\b|\bбар\b|kitchen|кухн|botany|hydropon|ботан|гідропон|janitor|прибиральн",
            FaxNameOptions),
    };

    private HashSet<string> JobDepartments(IEnumerable<string> jobs)
    {
        var departments = new HashSet<string>();
        foreach (var job in jobs)
        {
            if (_job.TryGetAllDepartments(job, out var found))
                departments.UnionWith(found.Select(department => department.ID));
        }

        return departments;
    }

    public static bool IsFaxRecipient(string faxName, IReadOnlyCollection<string>? departments)
    {
        if (CommandFaxes.IsMatch(faxName))
            return true;

        foreach (var (department, pattern) in DepartmentFaxes)
        {
            if ((departments == null || departments.Contains(department)) && pattern.IsMatch(faxName))
                return true;
        }

        return false;
    }

    private void SendCommandFax(string subject, string sender, string body, IReadOnlyCollection<string>? departments)
    {
        // Crewed stations only, not CentCom or other station-like maps.
        var grids = new HashSet<EntityUid>();
        foreach (var station in _stations.GetStationsSet())
        {
            if (HasComp<StationJobsComponent>(station) && TryComp<StationDataComponent>(station, out var data))
                grids.UnionWith(data.Grids);
        }

        var faxes = EntityQueryEnumerator<FaxMachineComponent, TransformComponent>();
        while (faxes.MoveNext(out var faxUid, out var fax, out var xform))
        {
            if (xform.GridUid is not { } grid || !grids.Contains(grid) || !IsFaxRecipient(fax.FaxName, departments))
                continue;

            var content = BuildCommandLetter(_paper.GetStationNumber(faxUid), subject, sender, body);
            var printout = new FaxPrintout(content, subject, null, null, "paper_stamp-centcom",
                new List<StampDisplayInfo>
                {
                    new()
                    {
                        StampedName = Loc.GetString("stamp-component-stamped-name-centcom"),
                        StampedColor = Color.FromHex("#006600"),
                        StampLargeIcon = "large_stamp-centcom",
                    },
                });
            _fax.Receive(faxUid, printout, null, fax);
        }
    }

    private string BuildCommandLetter(string station, string subject, string sender, string body)
    {
        var date = $"{DateTime.UtcNow.Day:00}/{DateTime.UtcNow.Month:00}/{PirateStationCalendar.CurrentYear:0000}";
        var time = GameTicker.RoundDuration().ToString("hh\\:mm\\:ss", CultureInfo.InvariantCulture);
        return Loc.GetString("pirate-rev-letter",
            ("station", FormattedMessage.EscapeText(station)),
            ("datetime", $"{date} {time}"),
            ("subject", FormattedMessage.EscapeText(subject)),
            ("sender", FormattedMessage.EscapeText(sender)),
            ("body", FormattedMessage.EscapeText(body)));
    }
}
