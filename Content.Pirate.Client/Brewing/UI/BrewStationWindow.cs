using System;
using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Pirate.Shared.Brewing;
using Content.Shared.Chemistry.Reagent;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Localization;
using Robust.Shared.IoC;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Pirate.Client.Brewing.UI;

public sealed class BrewStationWindow : DefaultWindow
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly RichTextLabel VoiceHintLabel;
    private readonly Button StartButton;
    private readonly SpriteView ContainerSprite;
    private readonly Label ContainerNameLabel;
    private readonly Label VolumeLabel;
    private readonly BoxContainer ReagentList;

    public event Action? StartPressed;

    public BrewStationWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(420, 420);
        Title = Loc.GetString("brew-station-ui-title");

        var root = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 6,
        };

        VoiceHintLabel = new RichTextLabel();
        StartButton = new Button();
        StartButton.OnPressed += _ => StartPressed?.Invoke();

        ContainerSprite = new SpriteView
        {
            SetSize = new Vector2(96, 96),
            Scale = new Vector2(3, 3),
        };

        ContainerNameLabel = new Label();
        VolumeLabel = new Label();
        ReagentList = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };

        root.AddChild(VoiceHintLabel);
        root.AddChild(StartButton);
        root.AddChild(ContainerSprite);
        root.AddChild(ContainerNameLabel);
        root.AddChild(VolumeLabel);
        var reagentScroll = new ScrollContainer
        {
            VerticalExpand = true,
        };
        reagentScroll.AddChild(ReagentList);

        var reagentPanel = new PanelContainer();
        reagentPanel.AddChild(reagentScroll);
        root.AddChild(reagentPanel);

        Contents.AddChild(root);
        UpdateState(new BrewStationBoundUserInterfaceState(Loc.GetString("brew-station-ui-title"), string.Empty, null, null, null, null, null, false));
    }

    public void UpdateState(BrewStationBoundUserInterfaceState state)
    {
        Title = state.Title;
        VoiceHintLabel.SetMessage(Loc.GetString("brew-station-ui-voice", ("commands", state.VoiceHint)));
        StartButton.Text = state.Mixing
            ? Loc.GetString("brew-station-ui-mixing")
            : Loc.GetString("brew-station-ui-start");
        StartButton.Disabled = state.Mixing;

        if (state.Container is { } container)
        {
            ContainerSprite.SetEntity(container);
            ContainerSprite.Visible = true;
        }
        else
        {
            ContainerSprite.SetEntity(null);
            ContainerSprite.Visible = false;
        }
        ContainerNameLabel.Text = state.ContainerName ?? Loc.GetString("brew-station-ui-empty");
        VolumeLabel.Text = state.Volume != null && state.MaxVolume != null
            ? Loc.GetString("brew-station-ui-volume", ("current", state.Volume), ("max", state.MaxVolume))
            : string.Empty;

        ReagentList.RemoveAllChildren();
        if (state.Reagents == null || state.Reagents.Count == 0)
        {
            ReagentList.AddChild(new Label { Text = Loc.GetString("brew-station-ui-no-reagents") });
            return;
        }

        foreach (var (reagent, quantity) in state.Reagents)
        {
            var reagentName = reagent.Prototype;
            if (_prototypeManager.TryIndex<ReagentPrototype>(reagent.Prototype, out var proto))
                reagentName = proto.LocalizedName;

            ReagentList.AddChild(new Label
            {
                Text = Loc.GetString("brew-station-ui-reagent", ("reagent", reagentName), ("amount", quantity)),
            });
        }
    }
}
