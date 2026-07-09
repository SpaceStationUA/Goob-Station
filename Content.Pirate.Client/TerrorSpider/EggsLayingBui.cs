using Content.Pirate.Shared.TerrorSpider;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.UserInterface;

namespace Content.Pirate.Client.TerrorSpider;

[UsedImplicitly]
public sealed partial class EggsLayingBui : BoundUserInterface
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IClyde _displayManager = default!;

    private EggsLayingMenu? _menu;

    public EggsLayingBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<EggsLayingMenu>();
        _menu.OnClose += Close;
        _menu.EggChosen += egg =>
        {
            SendMessage(new EggsLayingBuiMsg { Egg = egg });
            _menu.Close();
            Close();
        };

        var vpSize = _displayManager.ScreenSize;
        _menu.OpenCenteredAt(_inputManager.MouseScreenPosition.Position / vpSize);
    }

    protected override void UpdateState(BoundUserInterfaceState? state)
    {
    }
}
