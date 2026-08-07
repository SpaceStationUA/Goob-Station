// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Client.UserInterface.Controls;
using Content.Pirate.Shared.Heretic.Lock;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Client.Heretic.Lock;

[UsedImplicitly]
public sealed class EldritchIdBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out EldritchIdCardComponent? id))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(ConvertToButtons(id.Configs.ToList()));
        _menu.Open();
    }

    private IEnumerable<RadialMenuActionOption<EldritchIdConfiguration>> ConvertToButtons(
        IReadOnlyList<EldritchIdConfiguration> configs)
    {
        var models = new RadialMenuActionOption<EldritchIdConfiguration>[configs.Count];
        for (var i = 0; i < configs.Count; i++)
        {
            var config = configs[i];
            var proto = _proto.Index(config.CardPrototype);
            var jobSuffix = string.IsNullOrWhiteSpace(config.JobTitle) ? string.Empty : $" ({config.JobTitle})";
            var tooltip = string.IsNullOrWhiteSpace(config.FullName)
                ? Loc.GetString("access-id-card-component-owner-name-job-title-text", ("jobSuffix", jobSuffix))
                : Loc.GetString("access-id-card-component-owner-full-name-job-title-text",
                    ("fullName", config.FullName),
                    ("jobSuffix", jobSuffix));

            models[i] = new RadialMenuActionOption<EldritchIdConfiguration>(HandleRadialMenuClick, config)
            {
                IconSpecifier = new RadialMenuEntityPrototypeIconSpecifier(proto),
                ToolTip = tooltip,
            };
        }

        return models;
    }

    private void HandleRadialMenuClick(EldritchIdConfiguration config)
    {
        SendPredictedMessage(new EldritchIdMessage(config));
    }
}
