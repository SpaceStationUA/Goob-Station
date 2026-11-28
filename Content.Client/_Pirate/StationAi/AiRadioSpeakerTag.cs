// SPDX-FileCopyrightText: 2025 Moffstation
// SPDX-License-Identifier: MIT

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.StationAi;
using Content.Client._Funkystation.Radio;
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
    [Dependency] private IEntitySystemManager _systems = default!;

    public string Name => "airadiospeaker";

    // The chat highlighter processes ordinary name text before these clickable controls are built.
    public static FormattedMessage CreateLinks(FormattedMessage message)
    {
        var result = new FormattedMessage(message.Count);
        MarkupNode? link = null;
        var highlights = new Stack<MarkupParameter>();
        foreach (var node in message)
        {
            if (node.Name == "airadiolink")
            {
                link = node.Closing ? null : node;
                highlights.Clear();
                continue;
            }

            if (link != null && node.Name == "color")
            {
                if (node.Closing)
                    highlights.TryPop(out _);
                else
                    highlights.Push(node.Value);
                continue;
            }

            if (link != null && node.IsPlainText)
            {
                var attributes = new Dictionary<string, MarkupParameter>(link.Attributes);
                if (highlights.TryPeek(out var tint))
                    attributes["highlight"] = tint;
                result.PushTag(new MarkupNode("airadiospeaker", node.Value, attributes), selfClosing: true);
                continue;
            }

            if (node.Closing)
                result.Pop();
            else
                result.PushTag(node);
        }
        return result;
    }

    public bool TryCreateControl(MarkupNode node, [NotNullWhen(true)] out Control? control)
    {
        control = null;
        if (!node.Value.TryGetString(out var name) ||
            !node.Attributes.TryGetValue("target", out var value) || !value.TryGetString(out var target) ||
            !NetEntity.TryParse(target, out var speaker))
            return false;

        var color = Color.CornflowerBlue;
        if (node.Attributes.TryGetValue("highlight", out var highlight) && highlight.TryGetColor(out var highlighted))
            color = highlighted.Value;
        else if (node.Attributes.TryGetValue("channel", out var channel) && channel.TryGetString(out var channelId))
            color = _systems.TryGetEntitySystem<RadioChannelColorSystem>(out var colors) ? colors.GetColor(channelId) : ColorTag.DefaultColor;
        else if (node.Attributes.TryGetValue("color", out var tint) && tint.TryGetString(out var hex) && Color.TryFromHex(hex, out var parsed))
            color = parsed; // Older chat recordings carry the original channel tint.

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
