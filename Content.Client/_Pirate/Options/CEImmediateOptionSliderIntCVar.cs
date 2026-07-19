using Content.Client.Options.UI;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.Options;

public sealed class CEImmediateOptionSliderIntCVar : BaseOption
{
    private readonly IConfigurationManager _cfg;
    private readonly CVarDef<int> _cVar;
    private readonly OptionSlider _slider;
    private readonly Func<int, string> _format;
    private readonly int _minValue;
    private readonly int _maxValue;
    private readonly int _step;
    private int _loadedValue;
    private bool _hasLoaded;
    private bool _updating;

    private int Value
    {
        get => Snap(Math.Clamp((int) _slider.Slider.Value, _minValue, _maxValue));
        set
        {
            _updating = true;
            _slider.Slider.Value = Snap(Math.Clamp(value, _minValue, _maxValue));
            UpdateLabelValue();
            _updating = false;
        }
    }

    public CEImmediateOptionSliderIntCVar(
        OptionsTabControlRow controller,
        IConfigurationManager cfg,
        CVarDef<int> cVar,
        OptionSlider slider,
        int minValue,
        int maxValue,
        Func<int, string>? format = null,
        int step = 1)
        : base(controller)
    {
        if (minValue > maxValue)
            throw new ArgumentException($"minValue ({minValue}) must be <= maxValue ({maxValue}) for cvar '{cVar.Name}'.", nameof(minValue));
        if (step < 1)
            throw new ArgumentException($"step ({step}) must be >= 1 for cvar '{cVar.Name}'.", nameof(step));

        _cfg = cfg;
        _cVar = cVar;
        _slider = slider;
        _format = format ?? (value => value.ToString());
        _minValue = minValue;
        _maxValue = maxValue;
        _step = step;

        _slider.Slider.MinValue = minValue;
        _slider.Slider.MaxValue = maxValue;
        _slider.Slider.Rounded = true;

        _slider.Slider.OnValueChanged += _ =>
        {
            if (_updating)
            {
                UpdateLabelValue();
                return;
            }

            // Snap the grabber onto the nearest step notch so the slider only ever reports valid values.
            var snapped = Value;
            if ((int) _slider.Slider.Value != snapped)
            {
                _updating = true;
                _slider.Slider.Value = snapped;
                _updating = false;
            }

            UpdateLabelValue();
            _cfg.SetCVar(_cVar, snapped);
            ValueChanged();
        };
    }

    // Rounds a raw value to the nearest multiple of the configured step, measured from the minimum.
    private int Snap(int value)
    {
        if (_step <= 1)
            return value;

        var steps = (int) MathF.Round((value - _minValue) / (float) _step);
        return Math.Clamp(_minValue + steps * _step, _minValue, _maxValue);
    }

    public override void LoadValue()
    {
        if (!_hasLoaded)
        {
            _loadedValue = Math.Clamp(_cfg.GetCVar(_cVar), _minValue, _maxValue);
            _hasLoaded = true;
        }

        Value = _loadedValue;
        _cfg.SetCVar(_cVar, _loadedValue);
    }

    public override void SaveValue()
    {
        _loadedValue = Value;
        _cfg.SetCVar(_cVar, _loadedValue);
    }

    public override void ResetToDefault()
    {
        Value = _cVar.DefaultValue;
        _cfg.SetCVar(_cVar, Value);
    }

    public override bool IsModified()
    {
        return Value != _loadedValue;
    }

    public override bool IsModifiedFromDefault()
    {
        return Value != Math.Clamp(_cVar.DefaultValue, _minValue, _maxValue);
    }

    private void UpdateLabelValue()
    {
        _slider.ValueLabel.Text = _format(Value);
    }
}
