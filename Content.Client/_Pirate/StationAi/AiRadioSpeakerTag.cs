// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.StationAi;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.RichText;
using Robust.Shared.Input;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Client._Pirate.StationAi;

[UsedImplicitly]
public sealed class AiRadioSpeakerTag : IMarkupTagHandler
{
    public string Name => "airadiospeaker";

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!node.Value.TryGetString(out var name) ||
            !node.Attributes.TryGetValue("target", out var value) || !value.TryGetString(out var target) ||
            !NetEntity.TryParse(target, out var speaker))
            return false;

        var color = Color.CornflowerBlue;
        if (node.Attributes.TryGetValue("color", out var tint) && tint.TryGetString(out var hex) && Color.TryFromHex(hex, out var parsed))
            color = parsed;

        var label = new Label
        {
            Text = name,
            HorizontalAlignment = Control.HAlignment.Left,
            FontColorOverride = color,
            MouseFilter = Control.MouseFilterMode.Stop,
            DefaultCursorShape = Control.CursorShape.Hand,
            ToolTip = Loc.GetString("pirate-ai-radio-jump"),
        };
        label.OnMouseEntered += _ => label.FontColorOverride = Color.White;
        label.OnMouseExited += _ => label.FontColorOverride = color;
        label.OnKeyBindDown += args =>
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                IoCManager.Resolve<IEntityManager>().RaisePredictiveEvent(new AiRadioJumpEvent(speaker));
        };
        control = label;
        return true;
    }
}
