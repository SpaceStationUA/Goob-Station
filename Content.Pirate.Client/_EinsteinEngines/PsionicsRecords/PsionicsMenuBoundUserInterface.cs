/// <summary>
/// Pirate psionics bound user interface, adapted from the Goob wanted menu.
/// </summary>

using Content.Shared.Access.Systems;
using Content.Shared.PsionicsRecords;
using Robust.Client.Player;
using Robust.Shared.Random;

namespace Content.Pirate.Client.PsionicsRecords;

public sealed class PsionicsMenuBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _proto = default!;
    private readonly AccessReaderSystem _accessReader;

    private PsionicsMenu? _window;

    public PsionicsMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _accessReader = EntMan.System<AccessReaderSystem>();
    }
    protected override void Open()
    {
        base.Open();

        _window = new(Owner, _random, _accessReader, _proto);

        _window.OnStatusSelected += status =>
            SendMessage(new PsionicsRecordChangeStatus(status, null));
	        _window.OnDialogConfirmed += (status, reason) =>
	            SendMessage(new PsionicsRecordChangeStatus(status, reason));

	        _window.OnClose += Close;

	        // Request initial state for this psionics menu from the server.
	        SendMessage(new PsionicsMenuRequestState());

	        _window.OpenCentered();
    }
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PsionicsRecordsConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        _window?.Close();
    }
}

