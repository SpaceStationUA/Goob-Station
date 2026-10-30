// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Triage.MedicalRecords;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.Triage;

/// <summary>Delta-V triage controls embedded beside the local wound/organ analyzer.</summary>
public sealed class MedicalTriageControl : BoxContainer
{
    public event Action<TriageStatus>? OnStatusChanged;
    public event Action? OnClaim;
    private readonly Dictionary<TriageStatus, Button> _statuses = new();
    private readonly Button _claim = new();

    public MedicalTriageControl()
    {
        Orientation = LayoutOrientation.Vertical;
        Visible = false;
        Margin = new Thickness(8);
        var row = new BoxContainer();
        var group = new ButtonGroup();
        foreach (var status in Enum.GetValues<TriageStatus>())
        {
            var button = new Button
            {
                Text = Loc.GetString($"health-analyzer-window-triage-status-{status}"),
                ToolTip = Loc.GetString($"health-analyzer-window-triage-status-{status}.ToolTip"),
                Group = group,
                HorizontalExpand = true,
            };
            button.OnPressed += _ => OnStatusChanged?.Invoke(status);
            button.AddStyleClass("ButtonSquare");
            _statuses.Add(status, button);
            row.AddChild(button);
        }
        _statuses[TriageStatus.None].RemoveStyleClass("ButtonSquare");
        _statuses[TriageStatus.None].AddStyleClass("OpenRight");
        _statuses[TriageStatus.Expectant].RemoveStyleClass("ButtonSquare");
        _statuses[TriageStatus.Expectant].AddStyleClass("OpenLeft");
        AddChild(row);
        _claim.OnPressed += _ => OnClaim?.Invoke();
        AddChild(_claim);
    }

    public void Populate(HealthAnalyzerTriageUpdateMessage state)
    {
        Visible = state.Record != null;
        if (state.Record is not {} record)
            return;
        foreach (var (status, button) in _statuses)
        {
            button.Disabled = !state.Active;
            button.Pressed = status == record.Status;
        }
        _claim.Disabled = !state.Active;
        _claim.Text = record.ClaimedName is {} name
            ? Loc.GetString("health-analyzer-window-triage-unclaim", ("claimedBy", name))
            : Loc.GetString("health-analyzer-window-triage-claim");
    }
}
