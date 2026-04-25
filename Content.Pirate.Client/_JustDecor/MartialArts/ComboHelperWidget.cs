using System.Numerics;
using Content.Client.UserInterface.Controls;
using Content.Goobstation.Common.MartialArts;
using Content.Goobstation.Shared.MartialArts;
using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.IoC;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Pirate.Client._JustDecor.MartialArts;

public sealed class ComboHelperWidget : UIWidget
{
    private static readonly IReadOnlyDictionary<MartialArtsForms, string> MartialArtPrototypeIds =
        new Dictionary<MartialArtsForms, string>
        {
            [MartialArtsForms.CorporateJudo] = "CorporateJudo",
            [MartialArtsForms.CloseQuartersCombat] = "CloseQuartersCombat",
            [MartialArtsForms.SleepingCarp] = "SleepingCarp",
            [MartialArtsForms.Capoeira] = "Capoeira",
            [MartialArtsForms.KungFuDragon] = "KungFuDragon",
            [MartialArtsForms.Ninjutsu] = "Ninjutsu",
            [MartialArtsForms.HellRip] = "HellRip",
            [MartialArtsForms.LegendaryCloseQuartersCombat] = "LegendaryCloseQuartersCombat",
        };

    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private readonly SpriteSystem _spriteSystem;
    private readonly BoxContainer _root;
    private readonly BoxContainer _container;
    private readonly DragButton _dragHandle;

    private bool _dragging;
    private bool _customPosition;
    private Vector2 _dragOffset;

    public ComboHelperWidget()
    {
        IoCManager.InjectDependencies(this);
        _spriteSystem = _entManager.System<SpriteSystem>();

        LayoutContainer.SetAnchorAndMarginPreset(this, LayoutContainer.LayoutPreset.BottomWide, margin: 5);
        LayoutContainer.SetGrowVertical(this, LayoutContainer.GrowDirection.Begin);
        HorizontalAlignment = HAlignment.Center;
        VerticalAlignment = VAlignment.Bottom;
        MouseFilter = MouseFilterMode.Pass;

        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Bottom,
            SeparationOverride = 4,
            MouseFilter = MouseFilterMode.Pass
        };

        _dragHandle = new DragButton
        {
            Text = ":::",
            HorizontalAlignment = HAlignment.Center,
            MinSize = new Vector2(32, 20),
            MouseFilter = MouseFilterMode.Stop
        };
        _dragHandle.OnMouseDown += StartDragging;
        _dragHandle.OnMouseUp += StopDragging;
        _dragHandle.OnMouseMove += ContinueDragging;

        _container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalAlignment = HAlignment.Center,
            VerticalAlignment = VAlignment.Bottom,
            Margin = new Thickness(0),
            SeparationOverride = 5,
            MouseFilter = MouseFilterMode.Ignore
        };

        _root.AddChild(_dragHandle);
        _root.AddChild(_container);
        AddChild(_root);
    }

    public void UpdateFromComponent(ComboHelperComponent component)
    {
        _container.DisposeAllChildren();

        if (!component.Enabled)
        {
            Visible = false;
            return;
        }

        if (TryRenderPrototypeCombos(component) || TryRenderMartialArtCombos())
        {
            Visible = true;
            return;
        }

        Visible = false;
    }

    private void StartDragging(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick || Parent == null)
            return;

        if (!_customPosition)
        {
            var currentPosition = Position;
            LayoutContainer.SetAnchorPreset(this, LayoutContainer.LayoutPreset.TopLeft);
            LayoutContainer.SetPosition(this, currentPosition);
            _customPosition = true;
        }

        _dragging = true;
        _dragOffset = args.PointerLocation.Position / UIScale - Position;
        SetPositionLast();
    }

    private void StopDragging(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIClick)
            return;

        _dragging = false;
        _dragOffset = Vector2.Zero;
        UserInterfaceManager.KeyboardFocused?.ReleaseKeyboardFocus();
    }

    private void ContinueDragging(GUIMouseMoveEventArgs args)
    {
        if (!_dragging || Parent == null)
            return;

        var maxPosition = Vector2.Max(Parent.Size - Size, Vector2.Zero);
        var globalPos = Vector2.Clamp(args.GlobalPosition, Vector2.Zero, Parent.Size);
        var newPosition = Vector2.Clamp(globalPos - _dragOffset, Vector2.Zero, maxPosition);

        LayoutContainer.SetPosition(this, newPosition);
    }

    private bool TryRenderPrototypeCombos(ComboHelperComponent component)
    {
        if (component.Prototype == null)
            return false;

        if (!_proto.TryIndex(component.Prototype.Value, out CqcComboHelperPrototype? helperProto))
            return false;

        foreach (var entry in helperProto.Combos)
        {
            if (!_proto.TryIndex(entry.ComboId, out ComboPrototype? comboProto))
                continue;

            CreateComboRow(comboProto, entry.Name, entry.Icons);
        }

        return _container.ChildCount > 0;
    }

    private bool TryRenderMartialArtCombos()
    {
        if (_player.LocalEntity is not { } player)
            return false;

        if (!_entManager.TryGetComponent<MartialArtsKnowledgeComponent>(player, out var knowledge))
            return false;

        if (!MartialArtPrototypeIds.TryGetValue(knowledge.MartialArtsForm, out var prototypeId))
            return false;

        if (!_proto.TryIndex<MartialArtPrototype>(prototypeId, out var martialArt))
            return false;

        if (!_proto.TryIndex(martialArt.RoundstartCombos, out ComboListPrototype? comboList))
            return false;

        foreach (var comboId in comboList.Combos)
        {
            if (!_proto.TryIndex(comboId, out ComboPrototype? comboProto))
                continue;

            CreateComboRow(comboProto);
        }

        return _container.ChildCount > 0;
    }

    private void CreateComboRow(
        ComboPrototype comboProto,
        string? customName = null,
        List<SpriteSpecifier>? customIcons = null)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 2,
            HorizontalAlignment = HAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore
        };

        var labelText = string.IsNullOrEmpty(customName)
            ? Loc.GetString(comboProto.Name)
            : Loc.GetString(customName);

        row.AddChild(new Label
        {
            Text = labelText,
            Margin = new Thickness(0, 0, 5, 0),
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore
        });

        var icons = customIcons;
        if (icons == null || icons.Count == 0)
            icons = GetDefaultIcons(comboProto.AttackTypes);

        foreach (var icon in icons)
        {
            var texture = TryGetTexture(icon);
            if (texture == null)
                continue;

            row.AddChild(new TextureRect
            {
                TextureScale = new Vector2(0.5f, 0.5f),
                Stretch = TextureRect.StretchMode.KeepAspectCentered,
                MinSize = new Vector2(24, 24),
                Texture = texture,
                MouseFilter = MouseFilterMode.Ignore
            });
        }

        _container.AddChild(row);
    }

    private Texture? TryGetTexture(SpriteSpecifier icon)
    {
        try
        {
            return _spriteSystem.Frame0(icon);
        }
        catch (Exception ex)
        {
            Logger.ErrorS("ComboHelper", $"Failed to load texture: {ex.Message}");
            return null;
        }
    }

    private static List<SpriteSpecifier> GetDefaultIcons(List<ComboAttackType> attacks)
    {
        var icons = new List<SpriteSpecifier>();
        var basePath = new ResPath("/Textures/_Goobstation/Interface/Misc/intents.rsi");

        foreach (var attack in attacks)
        {
            var state = attack switch
            {
                ComboAttackType.Grab => "grab",
                ComboAttackType.Disarm => "disarm",
                ComboAttackType.Harm => "harm",
                ComboAttackType.HarmLight => "harmlight",
                ComboAttackType.Hug => "hug",
                _ => "help"
            };

            icons.Add(new SpriteSpecifier.Rsi(basePath, state));
        }

        return icons;
    }

    private sealed class DragButton : Button
    {
        public event Action<GUIBoundKeyEventArgs>? OnMouseDown;
        public event Action<GUIBoundKeyEventArgs>? OnMouseUp;
        public event Action<GUIMouseMoveEventArgs>? OnMouseMove;

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            base.MouseMove(args);
            OnMouseMove?.Invoke(args);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            base.KeyBindDown(args);

            if (args.Function == EngineKeyFunctions.UIClick)
                OnMouseDown?.Invoke(args);
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            base.KeyBindUp(args);

            if (args.Function == EngineKeyFunctions.UIClick)
                OnMouseUp?.Invoke(args);
        }
    }
}
