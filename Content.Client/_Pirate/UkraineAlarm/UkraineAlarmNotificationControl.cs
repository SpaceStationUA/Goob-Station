using System.Numerics;
using Content.Client.Stylesheets;
using Content.Shared._Pirate.UkraineAlarm;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._Pirate.UkraineAlarm;

public sealed class UkraineAlarmNotificationControl : PanelContainer
{
    public UkraineAlarmNotificationControl(string message, UkraineAlarmDangerLevel dangerLevel)
    {
        var accentColor = GetAccentColor(dangerLevel);

        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Top;
        Margin = new Thickness(0, 32, 0, 0);
        MinSize = new Vector2(440, 0);
        MouseFilter = MouseFilterMode.Ignore;
        PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.FromHex("#17191F").WithAlpha(0.8f),
            BorderColor = accentColor.WithAlpha(0.9f),
            BorderThickness = new Thickness(2),
            ContentMarginLeftOverride = 24,
            ContentMarginRightOverride = 24,
            ContentMarginTopOverride = 18,
            ContentMarginBottomOverride = 18,
        };

        var content = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            HorizontalExpand = true,
        };

        content.AddChild(new Label
        {
            Text = message,
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
            FontColorOverride = Color.FromHex("#F2F3F5"),
        });

        content.AddChild(new Label
        {
            Text = GetDangerText(dangerLevel),
            HorizontalAlignment = HAlignment.Center,
            HorizontalExpand = true,
            Align = Label.AlignMode.Center,
            FontColorOverride = accentColor,
            StyleClasses = { StyleClass.LabelHeading },
        });

        AddChild(content);
    }

    private static string GetDangerText(UkraineAlarmDangerLevel dangerLevel)
    {
        return dangerLevel switch
        {
            UkraineAlarmDangerLevel.Red => "ЧЕРВОНИЙ РІВЕНЬ НЕБЕЗПЕКИ",
            UkraineAlarmDangerLevel.Yellow => "ЖОВТИЙ РІВЕНЬ НЕБЕЗПЕКИ",
            _ => "ВІДБІЙ ПОВІТРЯНОЇ ТРИВОГИ",
        };
    }

    private static Color GetAccentColor(UkraineAlarmDangerLevel dangerLevel)
    {
        return dangerLevel switch
        {
            UkraineAlarmDangerLevel.Red => Color.FromHex("#FF4D4D"),
            UkraineAlarmDangerLevel.Yellow => Color.FromHex("#FFD54A"),
            _ => Color.FromHex("#AEB7C4"),
        };
    }
}
