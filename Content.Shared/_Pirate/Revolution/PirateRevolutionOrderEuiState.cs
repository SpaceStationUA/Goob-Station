// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Shared._Pirate.CriminalRecords;
using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Revolution;

[Serializable, NetSerializable]
public sealed record RevOrderCrew(NetEntity Mob, string Name);

[Serializable, NetSerializable]
public sealed record RevOrderGroup(string? Department, string Name, int DefaultAmount,
    int MaxAmount, List<RevOrderCrew> Crew);

[Serializable, NetSerializable]
public sealed record RevOrderOption(string Id, string Name, string? Unavailable, List<RevOrderGroup> Groups,
    string? FixedTarget = null, bool HasFine = false);

[Serializable, NetSerializable]
public sealed record RevOrderPreview(string Sender, string? Target, string? Effect, string Template,
    Dictionary<string, string> Values);

[Serializable, NetSerializable]
public sealed class PirateRevolutionOrderEuiState(int revision, List<RevOrderOption> options,
    RevOrderPreview? preview, string? status) : EuiStateBase
{
    public readonly int Revision = revision;
    public readonly List<RevOrderOption> Options = options;
    public readonly RevOrderPreview? Preview = preview;
    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public sealed class RevOrderRefresh(int revision) : EuiMessageBase
{
    public readonly int Revision = revision;
}

[Serializable, NetSerializable]
public sealed class RevOrderPrepare(int revision, int option, int group, int amount, int[] crew, int fine = 0) : EuiMessageBase
{
    public readonly int Revision = revision;
    public readonly int Option = option;
    public readonly int Group = group;
    public readonly int Amount = amount;
    public readonly int[] Crew = crew;
    public readonly int Fine = fine;
}

[Serializable, NetSerializable]
public sealed class RevOrderApply(int revision, string template) : EuiMessageBase
{
    public readonly int Revision = revision;
    public readonly string Template = template;
}

public static class RevOrderTaxFine
{
    public const int Min = 1;
    public const int Max = CriminalRecordCitations.MaxFine;
    public const int RandomMin = 500;
    public const int RandomMax = 1000;
    public const int Step = 50;
    public const int Default = 750;

    public static bool IsValid(int fine) => fine is >= Min and <= Max;
}

public static class RevOrderAnnouncement
{
    private const int MaxTemplateLength = 2048;
    private const int MaxAnnouncementLength = 8192;

    private static readonly Regex Placeholder = new(@"\$\{([^{}\r\n]*)\}");

    public static string Token(string name) => "${" + name + "}";

    public static string? Render(string template, IReadOnlyDictionary<string, string> values, out string? error)
    {
        if (template.Length > MaxTemplateLength)
        {
            error = Loc.GetString("pirate-rev-ui-template-too-long", ("max", MaxTemplateLength));
            return null;
        }

        string? unknown = null;
        var rendered = Placeholder.Replace(template, match =>
        {
            if (values.TryGetValue(match.Groups[1].Value, out var value))
                return value;

            unknown ??= match.Value;
            return match.Value;
        }).Trim();

        // The crew token can exceed the normal announcement limit.
        var maxLength = values.TryGetValue("crew", out var crew)
            ? Math.Max(MaxAnnouncementLength, crew.Length + MaxTemplateLength)
            : MaxAnnouncementLength;
        error = unknown != null
            ? Loc.GetString("pirate-rev-ui-unknown-token", ("token", unknown))
            : rendered.Length switch
            {
                0 => Loc.GetString("pirate-rev-ui-empty"),
                _ when rendered.Length > maxLength => Loc.GetString("pirate-rev-ui-preview-too-long", ("max", maxLength)),
                _ => null,
            };
        return error == null ? rendered : null;
    }
}
