using System.Numerics;
using Content.Client.Administration.UI.CustomControls;
using Content.Client.Message;
using Content.Client.Resources;
using Content.Shared._Pirate.Employment;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.IoC;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client._Pirate.Employment.UI;

public sealed class EmployerSelectionWindow : DefaultWindow
{
    private readonly IResourceCache _resources;
    private readonly RichTextLabel _description = new()
    {
        HorizontalExpand = true,
        VerticalAlignment = VAlignment.Top,
        RectClipContent = true,
    };
    private readonly Label _title = new()
    {
        HorizontalAlignment = HAlignment.Center,
        HorizontalExpand = true,
    };
    private readonly TextureRect _logo = new()
    {
        MinSize = new Vector2(150, 110),
        SetSize = new Vector2(150, 110),
        RectClipContent = true,
        HorizontalAlignment = HAlignment.Center,
        Stretch = TextureRect.StretchMode.KeepAspectCentered,
    };
    private readonly BoxContainer _departments = new() { Orientation = LayoutOrientation.Vertical };
    private readonly Dictionary<string, ContainerButton> _cards = new();
    private readonly Button _chooseButton = new() { HorizontalExpand = true };
    private string? _selectedEmployer;
    private EmployerPrototype? _viewedPrototype;

    public event Action<EmployerPrototype>? OnEmployerSelected;

    public EmployerSelectionWindow(IReadOnlyList<EmployerPrototype> employers, string? selected)
    {
        IoCManager.InjectDependencies(this);
        _resources = IoCManager.Resolve<IResourceCache>();
        _selectedEmployer = selected;
        Title = Loc.GetString("employment-employer-selector-title");
        MinSize = new Vector2(900, 560);
        SetSize = new Vector2(960, 620);

        var root = new BoxContainer
        {
            RectClipContent = true,
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(6),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        var listScroll = new ScrollContainer
        {
            MaxWidth = 288,
            HScrollEnabled = false,
            ReserveScrollbarSpace = true,
            VerticalExpand = true,
        };
        var list = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 4, Margin = new Thickness(2) };
        listScroll.AddChild(list);
        var listPanel = NanoPanel(new Vector2(288, 0));
        listPanel.HorizontalExpand = false;
        listPanel.MaxWidth = 288;
        listPanel.RectClipContent = true;
        var listContent = new BoxContainer { Orientation = LayoutOrientation.Vertical };
        listContent.AddChild(NanoHeader("employment-employer-selector-list-title"));
        listContent.AddChild(listScroll);
        listPanel.AddChild(listContent);
        root.AddChild(listPanel);

        var main = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, VerticalExpand = true, SeparationOverride = 8 };
        _title.AddStyleClass("LabelHeadingBigger");
        var titleHeader = NanoHeader(_title);
        main.AddChild(titleHeader);

        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 4,
        };
        var descriptionScroll = new ScrollContainer
        {
            HScrollEnabled = false,
            ReserveScrollbarSpace = true,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _description.Margin = new Thickness(6);
        descriptionScroll.AddChild(_description);
        body.AddChild(descriptionScroll);
        body.AddChild(new VSeparator());

        var side = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinSize = new Vector2(200, 0),
            MaxWidth = 200,
            VerticalExpand = true,
            SeparationOverride = 8,
            Margin = new Thickness(0, 4, 4, 4),
            RectClipContent = true,
        };
        side.AddChild(_logo);
        side.AddChild(NanoHeader("employment-employer-selector-departments"));
        side.AddChild(_departments);
        side.AddChild(new Control { VerticalExpand = true, MinHeight = 10 });
        side.AddChild(_chooseButton);
        _chooseButton.OnPressed += _ =>
        {
            if (_viewedPrototype != null)
                OnEmployerSelected?.Invoke(_viewedPrototype);
        };
        _chooseButton.Disabled = employers.Count == 0;
        body.AddChild(side);
        main.AddChild(body);
        var mainPanel = NanoPanel(Vector2.Zero);
        mainPanel.RectClipContent = true;
        mainPanel.AddChild(main);
        root.AddChild(mainPanel);
        var background = new PanelContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#1A1D24") },
        };
        background.AddChild(root);
        ContentsContainer.AddChild(background);

        foreach (var employer in employers)
        {
            var card = CreateCard(employer);
            card.OnPressed += _ => ViewEmployer(employer);
            _cards[employer.ID] = card;
            list.AddChild(card);

            if (employer.ID == selected)
                ViewEmployer(employer);
        }

        if (_viewedPrototype == null && employers.Count > 0)
            ViewEmployer(employers[0]);
    }

    private ContainerButton CreateCard(EmployerPrototype employer)
    {
        var card = new ContainerButton
        {
            MinSize = new Vector2(0, 82),
            RectClipContent = true,
            HorizontalExpand = true,
            StyleBoxOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#272B35"),
                BorderColor = Color.FromHex("#2E323D"),
                BorderThickness = new Thickness(1),
            }
        };
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(6),
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        row.AddChild(new Label
        {
            Text = WrapEmployerName(Loc.GetString(employer.NameKey)),
            VerticalAlignment = VAlignment.Center,
            VerticalExpand = true,
            MaxWidth = 190,
        });
        row.AddChild(new Control
        {
            HorizontalExpand = true,
        });
        row.AddChild(new TextureRect
        {
            Texture = GetLogo(employer),
            MinSize = new Vector2(56, 56),
            SetSize = new Vector2(56, 56),
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Center,
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
        });
        card.AddChild(row);
        return card;
    }

    private static string WrapEmployerName(string name, int maxLineLength = 20)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.Length > 0 && current.Length + word.Length + 1 > maxLineLength)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = current.Length == 0 ? word : $"{current} {word}";
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return string.Join('\n', lines);
    }

    private void ViewEmployer(EmployerPrototype employer)
    {
        _viewedPrototype = employer;
        _title.Text = Loc.GetString(employer.NameKey);
        _logo.Texture = GetLogo(employer);
        _description.SetMarkup(BuildEmployerProfile(employer));

        _departments.RemoveAllChildren();
        foreach (var department in employer.Departments)
            _departments.AddChild(new Label
            {
                Text = $"- {Loc.GetString(department)}",
                HorizontalAlignment = HAlignment.Center,
            });

        _chooseButton.Text = _selectedEmployer == employer.ID
                ? Loc.GetString("employment-employer-selector-selected", ("employer", Loc.GetString(employer.NameKey)))
                : Loc.GetString("employment-employer-selector-choose");
        _chooseButton.Disabled = _selectedEmployer == employer.ID;
        UpdateCardStyles();
    }

    private void UpdateCardStyles()
    {
        foreach (var (id, card) in _cards)
        {
            var style = (StyleBoxFlat) card.StyleBoxOverride!;
            style.BorderColor = id == _selectedEmployer
                ? Color.FromHex("#6F9FE0")
                : id == _viewedPrototype?.ID ? Color.FromHex("#6B7383") : Color.FromHex("#2E323D");
            style.BorderThickness = new Thickness(id == _selectedEmployer ? 3 : id == _viewedPrototype?.ID ? 2 : 1);
        }
    }

    private static PanelContainer NanoPanel(Vector2 minSize)
    {
        return new PanelContainer
        {
            MinSize = minSize,
            HorizontalExpand = true,
            VerticalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#21242D"),
                BorderColor = Color.FromHex("#2E323D"),
                BorderThickness = new Thickness(1),
            }
        };
    }

    private static PanelContainer NanoHeader(string key)
    {
        return NanoHeader(new Label { Text = Loc.GetString(key) });
    }

    private static PanelContainer NanoHeader(Label label)
    {
        label.FontColorOverride = Color.FromHex("#6F9FE0");
        label.HorizontalExpand = true;
        label.HorizontalAlignment = HAlignment.Center;
        return new PanelContainer
        {
            HorizontalExpand = true,
            PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = Color.FromHex("#272B35"),
                BorderColor = Color.FromHex("#6F9FE0"),
                BorderThickness = new Thickness(0, 0, 0, 2),
                ContentMarginLeftOverride = 4,
                ContentMarginRightOverride = 4,
                ContentMarginTopOverride = 9,
                ContentMarginBottomOverride = 8,
            },
            Children = { label }
        };
    }

    private Texture GetLogo(EmployerPrototype employer) => _resources.GetTexture(employer.LogoPath);

    private static string BuildEmployerProfile(EmployerPrototype employer)
    {
        var sections = new List<string>();
        AddEmployerInfoSection(sections, employer.DescriptionKey, "employment-employer-info-description-title");
        AddEmployerInfoSection(sections, employer.EmployeeRelationsKey, "employment-employer-info-relations-title");

        var recommendations = BuildBulletList(employer.RoleplayRecommendations, "#B8B8C0");
        if (!string.IsNullOrEmpty(recommendations))
            AddFormattedEmployerInfoSection(sections, "employment-employer-info-roleplay-title", recommendations);

        return string.Join("\n\n", sections);
    }

    private static void AddEmployerInfoSection(List<string> sections, string key, string titleKey)
    {
        if (string.IsNullOrWhiteSpace(key) || !Loc.TryGetString(key, out var text) || string.IsNullOrWhiteSpace(text))
            return;

        AddFormattedEmployerInfoSection(sections, titleKey, text);
    }

    private static void AddFormattedEmployerInfoSection(List<string> sections, string titleKey, string text)
    {
        sections.Add($"[color=#6F9FE0][font size=18]{Loc.GetString(titleKey)}[/font][/color]\n{text}");
    }

    private static string BuildBulletList(IEnumerable<string> keys, string color)
    {
        var bullets = new List<string>();
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || !Loc.TryGetString(key, out var text) || string.IsNullOrWhiteSpace(text))
                continue;

            bullets.Add($"[color={color}]\u2022 {text}[/color]");
        }

        return string.Join('\n', bullets);
    }

}
