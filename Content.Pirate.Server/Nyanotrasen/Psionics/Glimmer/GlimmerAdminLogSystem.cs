using Content.Server.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Psionics.Glimmer;

namespace Content.Server.Psionics.Glimmer;

/// <summary>
/// Writes glimmer changes to the admin logs, including how much it changed, the resulting value and the cause.
/// </summary>
public sealed class GlimmerAdminLogSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLog = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<GlimmerChangedLogEvent>(OnGlimmerChanged);
    }

    private void OnGlimmerChanged(GlimmerChangedLogEvent ev)
    {
        if (ev.ChangeType == GlimmerChangeType.Passive)
        {
            return;
        }

        var deltaText = ev.Delta >= 0 ? $"+{ev.Delta}" : ev.Delta.ToString();
        var sourceText = ev.Source is { } source && Exists(source)
            ? $", source: {ToPrettyString(source):entity}"
            : string.Empty;
        var message = $"Glimmer changed from {ev.OldGlimmer} to {ev.NewGlimmer} ({deltaText}) due to {ev.Reason}{sourceText}.";

        _adminLog.Add(LogType.Psionics, LogImpact.Low, $"{message}");
    }
}
