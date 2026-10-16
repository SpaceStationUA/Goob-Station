// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Graphics;
using Robust.Shared.Input;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.CustomMarkings.UI;

/// <summary>
/// Classic colour popup: a saturation/value box to drag around, a hue bar below it, and RGB, HSV and hex fields.
/// </summary>
public sealed class CustomMarkingColorPopup : Popup
{
    public Action<Color>? OnColorChanged;

    private readonly SvBox _sv;
    private readonly HueBar _hue;
    private readonly PanelContainer _chip;
    private readonly LineEdit _r;
    private readonly LineEdit _g;
    private readonly LineEdit _b;
    private readonly LineEdit _hField;
    private readonly LineEdit _sField;
    private readonly LineEdit _vField;
    private readonly LineEdit _hex;
    private bool _updating;

    private float _h;
    private float _s = 1f;
    private float _v = 1f;

    public Color Color
    {
        get => Color.FromHsv(new Vector4(_h, _s, _v, 1f));
        set
        {
            var hsv = Color.ToHsv(value);
            // Keep the previous hue for greys so the box does not jump to red
            if (hsv.Y > 0.001f && hsv.Z > 0.001f)
                _h = hsv.X;
            _s = hsv.Y;
            _v = hsv.Z;
            Refresh();
        }
    }

    public CustomMarkingColorPopup()
    {
        _sv = new SvBox(this) { MinSize = new Vector2(232, 160) };
        _hue = new HueBar(this) { MinSize = new Vector2(232, 18) };
        _chip = new PanelContainer { MinSize = new Vector2(28, 28), PanelOverride = new StyleBoxFlat(Color.White), VerticalAlignment = VAlignment.Center };
        _r = Field(3);
        _g = Field(3);
        _b = Field(3);
        _hField = Field(3);
        _sField = Field(3);
        _vField = Field(3);
        _hex = Field(9);
        _hex.HorizontalExpand = true;

        _r.OnTextChanged += _ => RgbEntered();
        _g.OnTextChanged += _ => RgbEntered();
        _b.OnTextChanged += _ => RgbEntered();
        _hField.OnTextChanged += _ => HsvEntered(_hField, 360, value => SetHsv(value, _s, _v));
        _sField.OnTextChanged += _ => HsvEntered(_sField, 100, value => SetHsv(_h, value, _v));
        _vField.OnTextChanged += _ => HsvEntered(_vField, 100, value => SetHsv(_h, _s, value));
        _hex.OnTextChanged += _ => HexEntered();

        var fields = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children =
            {
                _chip,
                FieldLabel("wf-color-red"), _r,
                FieldLabel("wf-color-green"), _g,
                FieldLabel("wf-color-blue"), _b,
            },
        };
        // Indented by the chip's width so the HSV fields sit under the RGB ones
        var hsv = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children =
            {
                new Control { MinWidth = 28 },
                FieldLabel("wf-color-hue"), _hField,
                FieldLabel("wf-color-saturation"), _sField,
                FieldLabel("wf-color-value"), _vField,
            },
        };
        var hex = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 4,
            Children = { FieldLabel("wf-color-hex"), _hex },
        };

        AddChild(new PanelContainer
        {
            StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorCard },
            Children =
            {
                new BoxContainer
                {
                    Orientation = BoxContainer.LayoutOrientation.Vertical,
                    SeparationOverride = 6,
                    Children = { _sv, _hue, fields, hsv, hex },
                },
            },
        });
    }

    private static LineEdit Field(int width)
    {
        return new LineEdit { MinWidth = width * 12 + 10, VerticalAlignment = VAlignment.Center };
    }

    private static Label FieldLabel(string loc)
    {
        return new Label { Text = Loc.GetString(loc), StyleClasses = { CustomMarkingStylesheet.StyleClassCreatorFieldLabel }, VerticalAlignment = VAlignment.Center };
    }

    private void SetHsv(float h, float s, float v)
    {
        _h = Math.Clamp(h, 0f, 1f);
        _s = Math.Clamp(s, 0f, 1f);
        _v = Math.Clamp(v, 0f, 1f);
        Refresh();
        OnColorChanged?.Invoke(Color);
    }

    /// <summary>Pushes the current colour into every control without re-triggering their handlers.</summary>
    private void Refresh()
    {
        _updating = true;
        var color = Color;
        _chip.PanelOverride = new StyleBoxFlat(color);
        _r.Text = color.RByte.ToString();
        _g.Text = color.GByte.ToString();
        _b.Text = color.BByte.ToString();
        _hField.Text = MathF.Round(_h * 360f).ToString("0");
        _sField.Text = MathF.Round(_s * 100f).ToString("0");
        _vField.Text = MathF.Round(_v * 100f).ToString("0");
        _hex.Text = color.ToHexNoAlpha();
        _sv.SetHue(_h);
        _updating = false;
    }

    private void RgbEntered()
    {
        if (_updating)
            return;
        if (!byte.TryParse(_r.Text, out var r) || !byte.TryParse(_g.Text, out var g) || !byte.TryParse(_b.Text, out var b))
            return;
        var hsv = Color.ToHsv(new Color(r, g, b));
        SetHsv(hsv.Y > 0.001f && hsv.Z > 0.001f ? hsv.X : _h, hsv.Y, hsv.Z);
    }

    /// <summary>
    /// One HSV field was edited: hue in degrees, saturation and value in percent. Only that component changes; the other
    /// two keep their exact values rather than the rounded ones their fields show.
    /// </summary>
    private void HsvEntered(LineEdit field, int max, Action<float> set)
    {
        if (_updating || !int.TryParse(field.Text, out var entered))
            return;
        set(Math.Clamp(entered, 0, max) / (float) max);
    }

    private void HexEntered()
    {
        if (_updating)
            return;
        var text = _hex.Text.Trim();
        if (!text.StartsWith('#'))
            text = "#" + text;
        if (text.Length != 7 || !Color.TryFromHex(text, out var color))
            return;
        var hsv = Color.ToHsv(color);
        SetHsv(hsv.Y > 0.001f && hsv.Z > 0.001f ? hsv.X : _h, hsv.Y, hsv.Z);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _sv.DisposeTexture();
    }

    /// <summary>Saturation left to right, value bottom to top, for the current hue.</summary>
    private sealed class SvBox : Control
    {
        private const int Resolution = 96;
        private readonly CustomMarkingColorPopup _owner;
        private OwnedTexture? _texture;
        private float _textureHue = -1f;
        private bool _dragging;

        public SvBox(CustomMarkingColorPopup owner)
        {
            _owner = owner;
            MouseFilter = MouseFilterMode.Stop;
        }

        public void SetHue(float hue)
        {
            if (Math.Abs(hue - _textureHue) < 0.0005f && _texture != null)
                return;
            _textureHue = hue;
            _texture?.Dispose();
            using var image = new Image<Rgba32>(Resolution, Resolution);
            for (var y = 0; y < Resolution; y++)
            {
                for (var x = 0; x < Resolution; x++)
                {
                    var c = Color.FromHsv(new Vector4(hue, x / (float) (Resolution - 1), 1f - y / (float) (Resolution - 1), 1f));
                    image[x, y] = new Rgba32(c.RByte, c.GByte, c.BByte, 255);
                }
            }
            _texture = IoCManager.Resolve<IClyde>().LoadTextureFromImage(image, "wolfgate-sv",
                new TextureLoadParameters { SampleParameters = new TextureSampleParameters { Filter = true } });
        }

        public void DisposeTexture()
        {
            _texture?.Dispose();
            _texture = null;
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            if (_texture == null)
                SetHue(_owner._h);
            var box = PixelSizeBox;
            handle.DrawTextureRect(_texture!, box);
            var pos = new Vector2(box.Left + _owner._s * box.Width, box.Top + (1f - _owner._v) * box.Height);
            handle.DrawCircle(pos, 7 * UIScale, Color.Black, false);
            handle.DrawCircle(pos, 5 * UIScale, Color.White, false);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;
            _dragging = true;
            Pick(args.RelativePixelPosition);
            args.Handle();
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                _dragging = false;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            if (_dragging)
                Pick(args.RelativePixelPosition);
        }

        private void Pick(Vector2 pixel)
        {
            var box = PixelSizeBox;
            _owner.SetHsv(_owner._h, pixel.X / box.Width, 1f - pixel.Y / box.Height);
        }
    }

    /// <summary>Hue strip with a marker; dragging sets the hue.</summary>
    private sealed class HueBar : Control
    {
        private static OwnedTexture? _texture;
        private readonly CustomMarkingColorPopup _owner;
        private bool _dragging;

        public HueBar(CustomMarkingColorPopup owner)
        {
            _owner = owner;
            MouseFilter = MouseFilterMode.Stop;
        }

        private static OwnedTexture Texture()
        {
            if (_texture != null)
                return _texture;
            using var image = new Image<Rgba32>(360, 1);
            for (var x = 0; x < 360; x++)
            {
                var c = Color.FromHsv(new Vector4(x / 360f, 1f, 1f, 1f));
                image[x, 0] = new Rgba32(c.RByte, c.GByte, c.BByte, 255);
            }
            return _texture = IoCManager.Resolve<IClyde>().LoadTextureFromImage(image, "wolfgate-hue",
                new TextureLoadParameters { SampleParameters = new TextureSampleParameters { Filter = true } });
        }

        protected override void Draw(DrawingHandleScreen handle)
        {
            var box = PixelSizeBox;
            handle.DrawTextureRect(Texture(), box);
            var x = box.Left + _owner._h * box.Width;
            handle.DrawRect(new UIBox2(x - 2 * UIScale, box.Top, x + 2 * UIScale, box.Bottom), Color.Black, false);
            handle.DrawRect(new UIBox2(x - 1 * UIScale, box.Top, x + 1 * UIScale, box.Bottom), Color.White, false);
        }

        protected override void KeyBindDown(GUIBoundKeyEventArgs args)
        {
            if (args.Function != EngineKeyFunctions.UIClick)
                return;
            _dragging = true;
            Pick(args.RelativePixelPosition);
            args.Handle();
        }

        protected override void KeyBindUp(GUIBoundKeyEventArgs args)
        {
            if (args.Function == EngineKeyFunctions.UIClick)
                _dragging = false;
        }

        protected override void MouseMove(GUIMouseMoveEventArgs args)
        {
            if (_dragging)
                Pick(args.RelativePixelPosition);
        }

        private void Pick(Vector2 pixel)
        {
            _owner.SetHsv(pixel.X / PixelSizeBox.Width, _owner._s, _owner._v);
        }
    }
}
