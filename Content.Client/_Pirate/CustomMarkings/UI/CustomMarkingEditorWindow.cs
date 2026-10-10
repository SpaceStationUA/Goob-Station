// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Client.Lobby;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Preferences;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// The pixel editor for one custom marking: four facings drawn over the character's body, one frame at a time for
/// an animated marking, then saved to the player's library under a name and a placement.
/// </summary>
public sealed partial class CustomMarkingEditorWindow : CustomMarkingWindow
{
    private enum Tool
    {
        Pencil,
        Eraser,
        Fill,
        Picker,

        /// <summary>Erases the body under the art instead of the art.</summary>
        BodyEraser,
    }

    private const int CanvasScale = 12;
    private const int FacingScale = 2;

    /// <summary>
    /// Width of the window's content, which the placement hint wraps to. It has to hold the tools, the canvas and
    /// the facing panel side by side: about 760 with the longest facing names.
    /// </summary>
    private const float ContentWidth = 790;

    private static readonly string[] FacingNames =
    {
        "wf-custom-marking-facing-south",
        "wf-custom-marking-facing-north",
        "wf-custom-marking-facing-east",
        "wf-custom-marking-facing-west",
    };

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IEntityManager _entMan = default!;

    private readonly CustomMarkingSystem _system;
    private readonly LobbyUIController _lobby;
    private readonly HumanoidCharacterProfile? _profile;

    /// <summary>The library entry being changed, or null for a new marking.</summary>
    private readonly CustomMarkingEntry? _entry;

    private readonly CustomMarkingSketch _sketch;
    private readonly CustomMarkingArt _opened;

    /// <summary>The body pixels erased by the character's other markings, and what those draw over solidly.</summary>
    private readonly byte[] _othersErase = new byte[CustomMarkingRules.EraseBytes];

    private readonly byte[] _othersSolid = new byte[CustomMarkingRules.EraseBytes];

    /// <summary>Everything erased from the body shown: by the other markings and by this one as it stands.</summary>
    private readonly byte[] _erase = new byte[CustomMarkingRules.EraseBytes];

    private readonly CustomMarkingCanvas _canvas;
    private readonly CustomMarkingBodySampler _sampler;
    private readonly Label _canvasHeading;
    private readonly CustomMarkingCanvas[] _facings = new CustomMarkingCanvas[CustomMarkingRules.Facings];
    private readonly CustomMarkingColorPicker _colour;
    private readonly Slider _opacity;
    private readonly LineEdit _name;
    private readonly OptionButton _placement;
    private readonly RichTextLabel _placementHint;
    private readonly CheckBox _showBody;
    private readonly CheckBox _showHair;
    private readonly CheckBox _showClothes;
    private readonly CustomMarkingIconButton _undoButton;
    private readonly CustomMarkingIconButton _redoButton;
    private readonly CustomMarkingIconButton _mirrorButton;
    private readonly Label _frameLabel;
    private readonly CustomMarkingIconButton _previousFrame;
    private readonly CustomMarkingIconButton _nextFrame;
    private readonly CustomMarkingIconButton _addFrame;
    private readonly CustomMarkingIconButton _removeFrame;
    private readonly CustomMarkingIconButton _playButton;
    private readonly FloatSpinBox _frameTime;
    private readonly Control _frameTimeRow;
    private readonly Button _saveButton;
    private readonly Label _status;

    private readonly Dictionary<Tool, CustomMarkingIconButton> _toolButtons = new();

    private Tool _tool = Tool.Pencil;
    private Vector2i? _last;
    private EntityUid? _body;
    private int _request = -1;
    private bool _pickedColor;

    /// <summary>The frame shown and drawn on.</summary>
    private int _frame;

    private bool _playing;

    /// <summary>How long the frame shown has been up while playing, in seconds.</summary>
    private float _playTime;

    /// <summary>Set while the frame time box is filled in from the art, which is not the player changing it.</summary>
    private bool _fillingTime;

    /// <summary>Whether the status line is saying that too much of the body is erased.</summary>
    private bool _erasedTooMuch;

    /// <summary>Raised once the server has saved the marking: the entry as it was opened, if any, and as saved.</summary>
    public Action<CustomMarkingEntry?, CustomMarkingEntry>? OnSaved;

    /// <param name="profile">The character whose body is drawn under the art, without the marking being edited.</param>
    public CustomMarkingEditorWindow(CustomMarkingEntry? entry, CustomMarkingArt art, string name, HumanoidCharacterProfile? profile)
    {
        IoCManager.InjectDependencies(this);
        _system = _entMan.System<CustomMarkingSystem>();
        _lobby = UserInterfaceManager.GetUIController<LobbyUIController>();
        _entry = entry;
        _sketch = new CustomMarkingSketch(art);
        _opened = art.Clone();
        _profile = profile;

        Title = Loc.GetString(entry == null ? "wf-custom-marking-editor-title-new" : "wf-custom-marking-editor-title-edit");
        Resizable = false;

        if (profile != null)
        {
            foreach (var worn in profile.CustomMarkings)
            {
                if (_system.TryGetErase(worn.Hash, out var mask))
                    CustomMarkingErase.Add(_othersErase, mask);

                if (_system.TryReadArt(worn.Hash, out var other))
                    CustomMarkingErase.Add(_othersSolid, other.Solid());
            }
        }

        // The canvas, and the body sampler the colour picker reads from
        _canvas = new CustomMarkingCanvas(CanvasScale, true) { Art = _sketch.Art, ShowGrid = true };
        _canvas.Stroke += OnStroke;
        _canvas.StrokeEnded += EndStroke;
        _canvasHeading = Heading(FacingNames[0]);
        _canvasHeading.HorizontalExpand = true;
        _canvasHeading.VAlign = Label.VAlignMode.Center;
        _sampler = new CustomMarkingBodySampler();
        UserInterfaceManager.RootControl.AddChild(_sampler);

        // Drawing tools, in a block two wide down the left of the canvas
        var toolGroup = new ButtonGroup();
        var drawing = ToolGrid();
        foreach (var tool in Enum.GetValues<Tool>())
        {
            if (tool == Tool.BodyEraser && !_system.EraseEnabled)
                continue;

            var icon = tool.ToString().ToLowerInvariant();
            var button = new CustomMarkingIconButton(icon, Loc.GetString($"wf-custom-marking-tool-{icon}"))
            {
                ToggleMode = true,
                Group = toolGroup,
                Pressed = tool == _tool,
            };
            button.AddStyleClass(CustomMarkingStylesheet.StyleClassCreatorToggle);
            button.OnPressed += _ => SetTool(tool);
            _toolButtons[tool] = button;
            drawing.AddChild(button);
        }

        var symmetry = new CustomMarkingIconButton("symmetry", Loc.GetString("wf-custom-marking-editor-symmetry")) { ToggleMode = true };
        symmetry.AddStyleClass(CustomMarkingStylesheet.StyleClassCreatorToggle);
        symmetry.OnToggled += args => SetMirror(args.Pressed);
        drawing.AddChild(symmetry);

        _undoButton = Command("undo", "wf-custom-marking-editor-undo", Undo);
        _redoButton = Command("redo", "wf-custom-marking-editor-redo", Redo);
        var history = ToolGrid();
        history.AddChild(_undoButton);
        history.AddChild(_redoButton);

        // What can be done to the facing shown: to its art on the frame shown, or with the body eraser picked, to
        // what is erased there
        _mirrorButton = Command("mirror", "wf-custom-marking-editor-mirror", () => Change(art =>
        {
            if (Masking)
                art.CopyErase(Facing, Opposite(Facing), true);
            else
                art.CopyFacing(_frame, Facing, Opposite(Facing), true);
        }));
        var facingTools = ToolGrid();
        facingTools.AddChild(Nudge("left", -1, 0));
        facingTools.AddChild(Nudge("right", 1, 0));
        facingTools.AddChild(Nudge("up", 0, -1));
        facingTools.AddChild(Nudge("down", 0, 1));
        facingTools.AddChild(Command("flip", "wf-custom-marking-editor-flip", () => Change(art =>
        {
            if (Masking)
                art.FlipErase(Facing, _sketch.MirrorAxis);
            else
                art.Flip(_frame, Facing, _sketch.MirrorAxis);
        })));
        facingTools.AddChild(_mirrorButton);
        facingTools.AddChild(Command("trash", "wf-custom-marking-editor-clear", () => Change(art =>
        {
            if (Masking)
                art.ClearErase(Facing);
            else
                art.Clear(_frame, Facing);
        })));

        var tools = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 10,
            Children = { drawing, history, facingTools },
        };

        // The four facings, each on the body, and what the canvas shows
        var facingGroup = new ButtonGroup();
        var facingGrid = new GridContainer { Columns = 2, HSeparationOverride = 4, VSeparationOverride = 4 };
        for (var i = 0; i < _facings.Length; i++)
        {
            var facing = i;
            _facings[i] = new CustomMarkingCanvas(FacingScale, false) { Art = _sketch.Art, Facing = i };
            var tile = new ContainerButton
            {
                StyleClasses = { ContainerButton.StyleClassButton, CustomMarkingStylesheet.StyleClassCreatorToggle },
                ToggleMode = true,
                Group = facingGroup,
                Pressed = i == 0,
                ToolTip = Loc.GetString(FacingNames[i]),
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = BoxContainer.LayoutOrientation.Vertical,
                        SeparationOverride = 2,
                        Children =
                        {
                            _facings[i],
                            new Label
                            {
                                Text = Loc.GetString(FacingNames[i]),
                                Align = Label.AlignMode.Center,
                                StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorFieldLabel },
                            },
                        },
                    },
                },
            };
            tile.OnPressed += _ => SetFacing(facing);
            facingGrid.AddChild(tile);
        }

        // The frames of an animated marking: which one is shown, adding and removing them, and how long each shows
        _frameLabel = new Label
        {
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorFieldLabel },
        };
        _previousFrame = Command("previous", "wf-custom-marking-editor-frame-previous", () => SetFrame(_frame - 1));
        _nextFrame = Command("next", "wf-custom-marking-editor-frame-next", () => SetFrame(_frame + 1));
        _addFrame = Command("add", "wf-custom-marking-editor-frame-add", AddFrame);
        _removeFrame = Command("remove", "wf-custom-marking-editor-frame-remove", RemoveFrame);
        _playButton = new CustomMarkingIconButton("play", Loc.GetString("wf-custom-marking-editor-frame-play")) { ToggleMode = true };
        _playButton.AddStyleClass(CustomMarkingStylesheet.StyleClassCreatorToggle);
        _playButton.OnToggled += args => SetPlaying(args.Pressed);
        _frameTime = new FloatSpinBox(0.05f, 2)
        {
            HorizontalExpand = true,
            IsValid = seconds => seconds * 1000 >= CustomMarkingRules.MinFrameTime && seconds * 1000 <= CustomMarkingRules.MaxFrameTime,
        };
        _frameTime.OnValueChanged += args => SetFrameTime(args.Value);
        _frameTimeRow = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            ToolTip = Loc.GetString("wf-custom-marking-editor-frame-time-tooltip",
                ("min", CustomMarkingRules.MinFrameTime / 1000f),
                ("max", CustomMarkingRules.MaxFrameTime / 1000f)),
            Children = { FieldLabel("wf-custom-marking-editor-frame-time"), _frameTime },
        };

        var frameHeading = Heading("wf-custom-marking-editor-frames");
        frameHeading.HorizontalExpand = true;
        var frames = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            // A server that only allows still markings shows the frames of one made before, to take them out.
            Visible = MaxFrames > 1 || art.Frames > 1,
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    Children = { frameHeading, _frameLabel },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 4,
                    Children = { _previousFrame, _nextFrame, _addFrame, _removeFrame, _playButton },
                },
                _frameTimeRow,
            },
        };

        _showBody = new CheckBox { Text = Loc.GetString("wf-custom-marking-editor-show-body"), Pressed = true };
        _showBody.OnPressed += _ => UpdateBody();
        _showClothes = new CheckBox { Text = Loc.GetString("wf-custom-marking-editor-show-clothes") };
        _showClothes.OnPressed += _ => ReloadBody();
        var showGrid = new CheckBox { Text = Loc.GetString("wf-custom-marking-editor-show-grid"), Pressed = true };
        showGrid.OnPressed += _ => _canvas.ShowGrid = showGrid.Pressed;
        _showHair = new CheckBox
        {
            Text = Loc.GetString("wf-custom-marking-editor-show-hair"),
            ToolTip = Loc.GetString("wf-custom-marking-editor-show-hair-tooltip"),
            Pressed = true,
        };
        _showHair.OnPressed += _ => UpdateBody();

        var view = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
            Children =
            {
                Heading("wf-custom-marking-editor-facings"),
                facingGrid,
                new Control { MinHeight = 4 },
                frames,
                new Control { VerticalExpand = true },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _showBody, _showClothes },
                },
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children = { _showHair, showGrid },
                },
            },
        };

        // Colour: the palette, the character's own colours beside it, and the opacity
        _colour = new CustomMarkingColorPicker { Color = Color.Black, VerticalAlignment = VAlignment.Center };
        _colour.OnColorChanged += _ => DrawAgain();
        _opacity = new Slider { MinValue = 5, MaxValue = 100, Value = 100, MinWidth = 120, VerticalAlignment = VAlignment.Center };
        var colours = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Children = { _colour },
        };
        if (profile != null)
        {
            colours.AddChild(new GridContainer
            {
                Columns = 2,
                HSeparationOverride = 2,
                VSeparationOverride = 2,
                VerticalAlignment = VAlignment.Center,
                Children =
                {
                    Swatch(profile.Appearance.SkinColor, "wf-custom-marking-editor-colour-skin"),
                    Swatch(profile.Appearance.HairColor, "wf-custom-marking-editor-colour-hair"),
                    Swatch(profile.Appearance.FacialHairColor, "wf-custom-marking-editor-colour-facial-hair"),
                    Swatch(profile.Appearance.EyeColor, "wf-custom-marking-editor-colour-eyes"),
                },
            });
        }

        colours.AddChild(new Control { HorizontalExpand = true });
        colours.AddChild(FieldLabel("wf-custom-marking-editor-opacity"));
        colours.AddChild(_opacity);

        // Name and placement, with what the placement means under them
        _name = new LineEdit
        {
            Text = name,
            MinWidth = 180,
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("wf-custom-marking-default-name"),
            IsValid = text => text.Length <= CustomMarkingRules.MaxNameLength,
        };
        _placement = new OptionButton { MinWidth = 170 };
        foreach (var placement in Enum.GetValues<CustomMarkingPlacement>())
        {
            _placement.AddItem(Loc.GetString(PlacementName(placement)), (int) placement);
        }

        _placement.SelectId((int) (entry?.Placement ?? CustomMarkingPlacement.Skin));
        _placement.OnItemSelected += args =>
        {
            _placement.SelectId(args.Id);
            UpdatePlacement();
        };
        _placementHint = new RichTextLabel { HorizontalExpand = true };

        _status = new Label { ClipText = true, HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
        _saveButton = new Button { Text = Loc.GetString("wf-custom-marking-editor-save"), StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorPrimary } };
        _saveButton.OnPressed += _ => Save();
        var cancel = new Button { Text = Loc.GetString("wf-custom-marking-editor-cancel") };
        cancel.OnPressed += _ => Close();

        Contents.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            SetWidth = ContentWidth,
            Children =
            {
                Card(new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 6,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            SeparationOverride = 6,
                            Children =
                            {
                                FieldLabel("wf-custom-marking-editor-name"),
                                _name,
                                new Control { MinWidth = 6 },
                                FieldLabel("wf-custom-marking-editor-placement"),
                                _placement,
                            },
                        },
                        _placementHint,
                    },
                }, true),
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Children =
                    {
                        Card(new BoxContainer
                        {
                            Orientation = BoxContainer.LayoutOrientation.Horizontal,
                            SeparationOverride = 8,
                            Children =
                            {
                                tools,
                                new BoxContainer
                                {
                                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                                    SeparationOverride = 6,
                                    Children = { _canvasHeading, _canvas },
                                },
                            },
                        }, false),
                        Card(view, true),
                    },
                },
                Card(colours, true),
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Horizontal,
                    SeparationOverride = 6,
                    Children = { _status, cancel, _saveButton },
                },
            },
        });

        _system.SaveAnswered += OnSaveAnswered;
        OnClose += () =>
        {
            _system.SaveAnswered -= OnSaveAnswered;
            DeleteBody();
            _sampler.Orphan();
        };

        ReloadBody();
        UpdatePlacement();
        UpdateErase();
        UpdateButtons();
    }

    private CustomMarkingArt Art => _sketch.Art;

    private int Facing => _canvas.Facing;

    private CustomMarkingPlacement Placement => (CustomMarkingPlacement) _placement.SelectedId;

    /// <summary>Most frames the server lets a marking be saved with.</summary>
    private int MaxFrames => Math.Clamp(_cfg.GetCVar(CustomMarkingCVars.MaxFrames), 1, CustomMarkingRules.MaxFrames);

    /// <summary>Whether the buttons that change a facing work on what is erased from the body, not on the art.</summary>
    private bool Masking => _tool == Tool.BodyEraser;

    public static string PlacementName(CustomMarkingPlacement placement)
    {
        return $"wf-custom-marking-placement-{placement.ToString().ToLowerInvariant()}";
    }

    private static Label Heading(string loc)
    {
        return new Label { Text = Loc.GetString(loc), StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorHeading } };
    }

    private static Label FieldLabel(string loc)
    {
        return new Label
        {
            Text = Loc.GetString(loc),
            VerticalAlignment = VAlignment.Center,
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorFieldLabel },
        };
    }

    /// <summary>Groups editor controls using the character creator's inset panels.</summary>
    private static PanelContainer Card(Control content, bool expand)
    {
        return new PanelContainer
        {
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorGroup },
            HorizontalExpand = expand,
            Children = { content },
        };
    }

    /// <summary>A block of icon buttons, two to a row.</summary>
    private static GridContainer ToolGrid()
    {
        return new GridContainer { Columns = 2, HSeparationOverride = 4, VSeparationOverride = 4 };
    }

    private static CustomMarkingIconButton Command(string icon, string loc, Action act)
    {
        var button = new CustomMarkingIconButton(icon, Loc.GetString(loc));
        button.OnPressed += _ => act();
        return button;
    }

    private CustomMarkingIconButton Nudge(string direction, int dx, int dy)
    {
        return Command(direction, $"wf-custom-marking-editor-nudge-{direction}", () => Change(art =>
        {
            if (Masking)
                art.ShiftErase(Facing, dx, dy);
            else
                art.Shift(_frame, Facing, dx, dy);
        }));
    }

    private Control Swatch(Color color, string tooltip)
    {
        var swatch = new ContainerButton
        {
            StyleClasses = { CustomMarkingColorPicker.StyleClassSwatch },
            ToolTip = Loc.GetString(tooltip),
            Children =
            {
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat(color.WithAlpha(1f)),
                    MinSize = new Vector2(20, 20),
                    Margin = new Thickness(2),
                },
            },
        };
        swatch.OnPressed += _ => _colour.Color = color.WithAlpha(1f);
        return swatch;
    }

    /// <summary>Turns mirror drawing on or off, with a line on the canvas where the mirror stands.</summary>
    private void SetMirror(bool on)
    {
        _sketch.Mirror = on;
        UpdateMirror();
    }

    /// <summary>Stands the mirror on the middle of the body in the facing shown, read off its torso.</summary>
    private void UpdateMirror()
    {
        var axis = CustomMarkingSketch.DefaultMirrorAxis;
        if (_body is { } body
            && _entMan.TryGetComponent(body, out SpriteComponent? sprite)
            && _system.MirrorAxis((body, sprite), Facing) is { } middle)
            axis = middle;

        _sketch.MirrorAxis = axis;
        _canvas.MirrorAxis = _sketch.Mirror ? axis : null;
    }

    private void SetTool(Tool tool)
    {
        _tool = tool;
        _sampler.Active = tool == Tool.Picker;
        _canvas.ShowErase = Masking;
    }

    /// <summary>Goes back to the pencil once a colour is chosen, from the palette or off the canvas.</summary>
    private void DrawAgain()
    {
        if (_tool is not (Tool.Picker or Tool.Eraser))
            return;

        SetTool(Tool.Pencil);
        _toolButtons[Tool.Pencil].Pressed = true;
    }

    private Rgba32 Ink()
    {
        return CustomMarkingArt.ToPixel(_colour.Color.WithAlpha(_opacity.Value / 100f));
    }

    private void SetFacing(int facing)
    {
        _canvas.Facing = facing;
        _sampler.Facing = facing;
        _canvasHeading.Text = Loc.GetString(FacingNames[facing]);
        UpdateMirror();
        UpdateButtons();
    }

    /// <summary>The other side view, which is this one seen in a mirror.</summary>
    private static int Opposite(int facing)
    {
        return facing == CustomMarkingArt.East ? CustomMarkingArt.West : CustomMarkingArt.East;
    }

    private void OnStroke(Vector2i pixel, bool erase, bool start)
    {
        if (start)
            StopPlaying();

        if (Masking)
        {
            if (start)
            {
                _sketch.Begin();
                _last = null;
            }

            // Left erases the body and right brings it back.
            _sketch.EraseLine(Facing, _last ?? pixel, pixel, !erase);
            _last = pixel;
            UpdateErase();
            return;
        }

        var tool = erase ? Tool.Eraser : _tool;
        var inFrame = CustomMarkingArt.InFrame(pixel.X, pixel.Y);

        if (tool == Tool.Picker)
        {
            if (start && inFrame)
                Pick(pixel);

            return;
        }

        if (start)
        {
            _sketch.Begin();
            _last = null;
        }

        if (tool == Tool.Fill)
        {
            if (start)
                _sketch.Fill(_frame, Facing, pixel.X, pixel.Y, Ink());

            return;
        }

        // A fast drag skips pixels, so each move draws the line from the last one.
        _sketch.Line(_frame, Facing, _last ?? pixel, pixel, tool == Tool.Eraser ? default : Ink());
        _last = pixel;
    }

    /// <summary>Takes the colour at a pixel: what is drawn there, or else the body showing through.</summary>
    private void Pick(Vector2i pixel)
    {
        if (Art.GetPixel(_frame, Facing, pixel.X, pixel.Y) is { A: > 0 } drawn)
        {
            _colour.Color = CustomMarkingArt.ToColor(drawn).WithAlpha(1f);
            _opacity.Value = MathF.Max(_opacity.MinValue, drawn.A * 100f / byte.MaxValue);
        }
        else if (_sampler.TryGetColor(pixel.X, pixel.Y, out var body))
        {
            _colour.Color = body;
            _opacity.Value = _opacity.MaxValue;
        }
        else
        {
            return;
        }

        // Pirate: keep this gesture in picker mode until the mouse is released.
        _pickedColor = true;
    }

    private void EndStroke()
    {
        _last = null;
        _sketch.End();
        if (_pickedColor)
        {
            _pickedColor = false;
            DrawAgain();
        }
        Changed();
    }

    /// <summary>Runs one edit as a single undo step.</summary>
    private void Change(Action<CustomMarkingArt> edit)
    {
        _sketch.Change(edit);
        Changed();
    }

    private void Undo()
    {
        _sketch.Undo();
        Changed();
    }

    private void Redo()
    {
        _sketch.Redo();
        Changed();
    }

    /// <summary>Brings everything that follows the art up to date with it.</summary>
    private void Changed()
    {
        UpdateErase();
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        _undoButton.Disabled = !_sketch.CanUndo;
        _redoButton.Disabled = !_sketch.CanRedo;
        _mirrorButton.Disabled = Facing is CustomMarkingArt.South or CustomMarkingArt.North;

        // Undoing can take away the frame that was shown.
        var frames = Art.Frames;
        if (_frame >= frames)
            ShowFrame(frames - 1);

        var animated = frames > 1;
        if (!animated)
            StopPlaying();

        _frameLabel.Text = Loc.GetString("wf-custom-marking-editor-frame-count", ("frame", _frame + 1), ("frames", frames));
        _previousFrame.Disabled = !animated;
        _nextFrame.Disabled = !animated;
        _removeFrame.Disabled = !animated;
        _playButton.Disabled = !animated;
        _addFrame.Disabled = frames >= MaxFrames;
        _frameTimeRow.Visible = animated;
        FillFrameTime();
    }

    private void FillFrameTime()
    {
        _fillingTime = true;
        _frameTime.Value = Art.GetFrameTime(_frame) / 1000f;
        _fillingTime = false;
    }

    /// <summary>Shows a frame on the canvas and the facings, without touching whether the frames are playing.</summary>
    private void ShowFrame(int frame)
    {
        _frame = Math.Clamp(frame, 0, Art.Frames - 1);
        _canvas.ArtFrame = _frame;
        foreach (var tile in _facings)
        {
            tile.ArtFrame = _frame;
        }
    }

    /// <summary>Goes to a frame to draw on it, round from the last to the first and back.</summary>
    private void SetFrame(int frame)
    {
        StopPlaying();
        var frames = Art.Frames;
        ShowFrame((frame % frames + frames) % frames);
        UpdateButtons();
    }

    /// <summary>Adds a frame after the one shown, as a copy of it, and goes to it.</summary>
    private void AddFrame()
    {
        StopPlaying();
        var added = -1;
        Change(art => added = art.AddFrame(_frame, MaxFrames));
        if (added >= 0)
            SetFrame(added);
    }

    private void RemoveFrame()
    {
        StopPlaying();
        Change(art => art.RemoveFrame(_frame));
    }

    private void SetFrameTime(float seconds)
    {
        if (_fillingTime)
            return;

        var time = (int) MathF.Round(seconds * 1000);
        Change(art => art.SetFrameTime(_frame, time));
    }

    private void SetPlaying(bool playing)
    {
        _playing = playing && Art.Frames > 1;
        _playTime = 0;
        _playButton.Pressed = _playing;
    }

    private void StopPlaying()
    {
        if (_playing)
            SetPlaying(false);
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!_playing || Art.Frames < 2)
            return;

        // At most one lap a frame: after a hitch the frames skip ahead rather than spin here.
        _playTime += args.DeltaSeconds;
        for (var i = 0; i < Art.Frames; i++)
        {
            var shown = Art.GetFrameTime(_frame) / 1000f;
            if (_playTime < shown)
                break;

            _playTime -= shown;
            ShowFrame((_frame + 1) % Art.Frames);
            _frameLabel.Text = Loc.GetString("wf-custom-marking-editor-frame-count", ("frame", _frame + 1), ("frames", Art.Frames));
            FillFrameTime();
        }
    }

    private void UpdatePlacement()
    {
        _canvas.Placement = Placement;
        foreach (var tile in _facings)
        {
            tile.Placement = Placement;
        }

        var reach = Loc.GetString("wf-custom-marking-editor-reach", ("margin", CustomMarkingSections.Margin));
        _placementHint.SetMessage(FormattedMessage.FromUnformatted($"{Loc.GetString(PlacementName(Placement) + "-hint")} {reach}"));
        UpdateSections();
    }

    /// <summary>Keeps drawing to the body shown and the margin around it, and darkens the rest of the canvas.</summary>
    private void UpdateSections()
    {
        byte[]? sections = null;
        if (_body is { } body && _entMan.TryGetComponent(body, out SpriteComponent? sprite))
            sections = _system.GetSections((body, sprite), Placement);

        _canvas.Sections = sections;
        foreach (var tile in _facings)
        {
            tile.Sections = sections;
        }

        _sketch.Allowed = sections == null
            ? null
            : (facing, x, y) => sections[CustomMarkingSections.Index(facing, x, y)] != CustomMarkingSections.None;
    }

    /// <summary>
    /// Works out what is erased from the body shown, which the canvases draw it without, and says so when it is
    /// more than a body may lose.
    /// </summary>
    private void UpdateErase()
    {
        if (!_system.EraseEnabled)
            return;

        _othersErase.CopyTo(_erase, 0);
        CustomMarkingErase.Add(_erase, Art.Erase);

        var tooMuch = false;
        if (Art.HasErase()
            && _body is { } body
            && _entMan.TryGetComponent(body, out SpriteComponent? sprite)
            && _system.GetBodyMask((body, sprite)) is { } bodyMask)
        {
            var covered = Art.Solid();
            CustomMarkingErase.Add(covered, _othersSolid);
            tooMuch = !CustomMarkingErase.LeavesEnough(bodyMask, _erase, covered);
        }

        if (tooMuch == _erasedTooMuch)
            return;

        _erasedTooMuch = tooMuch;
        if (tooMuch)
            SetStatus(Loc.GetString("wf-custom-marking-editor-erase-too-much", ("percent", CustomMarkingErase.MinKeptPercent)), true);
        else
            SetStatus(string.Empty, false);
    }

    /// <summary>Remakes the doll drawn under the art, dressed for the character's job or not.</summary>
    private void ReloadBody()
    {
        DeleteBody();
        if (_profile != null)
            _body = _lobby.LoadProfileEntity(_profile, null, _showClothes.Pressed);

        UpdateBody();
        UpdateMirror();
        UpdateSections();
    }

    private void UpdateBody()
    {
        var shown = _showBody.Pressed ? _body : null;
        var erase = _system.EraseEnabled ? _erase : null;
        var hideHair = !_showHair.Pressed;
        _canvas.Body = shown;
        _canvas.Erase = erase;
        _canvas.HideHair = hideHair;
        _sampler.Body = shown;
        _sampler.Erase = erase;
        _sampler.HideHair = hideHair;
        foreach (var tile in _facings)
        {
            tile.Body = shown;
            tile.Erase = erase;
            tile.HideHair = hideHair;
        }
    }

    private void DeleteBody()
    {
        var body = _body;
        _body = null;
        UpdateBody();
        if (body is { } doll)
            _entMan.DeleteEntity(doll);
    }

    private void Save()
    {
        if (_request >= 0)
            return;

        if (Art.IsBlank() && !Art.HasErase())
        {
            SetStatus(Loc.GetString("wf-custom-marking-error-blank"), true);
            return;
        }

        // Unchanged art isn't sent again: the server keeps what the entry has.
        var changed = _entry == null || !Art.Same(_opened);
        // Pirate: the queued request owns its pixels while the player continues editing.
        var submitted = changed ? Art.Clone() : null;
        _request = _system.Save(_entry?.Id ?? 0, CustomMarkingRules.CleanName(_name.Text), Placement, submitted);
        _saveButton.Disabled = true;
        SetStatus(Loc.GetString("wf-custom-marking-editor-saving"), false);
    }

    private void OnSaveAnswered(CustomMarkingSaveResultEvent ev)
    {
        if (ev.Request != _request)
            return;

        _request = -1;
        _saveButton.Disabled = false;
        if (ev.Entry is not { } saved)
        {
            SetStatus(Loc.GetString(ev.Error ?? "wf-custom-marking-error-failed"), true);
            return;
        }

        OnSaved?.Invoke(_entry, saved);
        Close();
    }

    private void SetStatus(string message, bool error)
    {
        _status.Text = message;
        if (error)
            _status.StyleClasses.Add("Danger");
        else
            _status.StyleClasses.Remove("Danger");
    }
}
