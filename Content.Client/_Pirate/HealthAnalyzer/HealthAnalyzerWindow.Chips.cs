// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.HealthAnalyzer.UI;

public sealed partial class HealthAnalyzerWindow
{
    private void DrawOrganChips(NetEntity organ, Dictionary<NetEntity, List<NetEntity>>? chips)
    {
        if (chips == null || !chips.TryGetValue(organ, out var installed) || installed.Count == 0)
            return;

        var container = new BoxContainer
        {
            Align = BoxContainer.AlignMode.Begin,
            Orientation = BoxContainer.LayoutOrientation.Vertical,
        };

        foreach (var chip in installed)
        {
            TryGetEntityName(chip, out var name);

            var row = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                VerticalAlignment = VAlignment.Center,
            };

            row.AddChild(CreateDiagnosticItemLabel(" - "));

            var chipEnt = _entityManager.GetEntity(chip);
            if (_entityManager.EntityExists(chipEnt))
            {
                var sprite = new SpriteView
                {
                    SetSize = new Vector2(30, 30),
                    OverrideDirection = Direction.South,
                };
                sprite.SetEntity(chipEnt);
                row.AddChild(sprite);
            }

            row.AddChild(CreateDiagnosticItemLabel(name));
            container.AddChild(row);
        }

        GroupsContainer.AddChild(container);
    }
}
