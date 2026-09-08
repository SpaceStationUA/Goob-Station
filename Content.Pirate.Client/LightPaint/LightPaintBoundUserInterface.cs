using Content.Pirate.Shared.LightPaint;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.LightPaint;

[UsedImplicitly]
public sealed class LightPaintBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private LightPaintWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<LightPaintWindow>();

        if (EntMan.TryGetComponent<LightPaintComponent>(Owner, out var paint))
            _window.SetColor(paint.Color);

        _window.OnColorSelected += color => SendPredictedMessage(new LightPaintColorSelectedMessage(color));
    }
}
