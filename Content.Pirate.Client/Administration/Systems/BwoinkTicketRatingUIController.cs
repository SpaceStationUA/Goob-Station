using Content.Client.Administration.Systems;
using Content.Pirate.Client.Administration.UI.Bwoink;
using Content.Shared.Administration;
using JetBrains.Annotations;
using Robust.Client.UserInterface.Controllers;

namespace Content.Pirate.Client.Administration.Systems;

[UsedImplicitly]
public sealed class BwoinkTicketRatingUIController : UIController, IOnSystemChanged<BwoinkSystem>
{
    private BwoinkSystem? _bwoinkSystem;
    private AHelpRatingWindow? _ratingWindow;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<BwoinkTicketRatingPrompt>(TicketRatingPrompted);
    }

    public void OnSystemLoaded(BwoinkSystem system)
    {
        _bwoinkSystem = system;
    }

    public void OnSystemUnloaded(BwoinkSystem system)
    {
        _bwoinkSystem = null;
        _ratingWindow?.Dispose();
        _ratingWindow = null;
    }

    private void TicketRatingPrompted(BwoinkTicketRatingPrompt args, EntitySessionEventArgs session)
    {
        _ratingWindow?.Dispose();
        _ratingWindow = new AHelpRatingWindow(args.CurrentRating, args.AdminName);
        _ratingWindow.RatingSelected += rating =>
        {
            _bwoinkSystem?.SubmitTicketRating(rating);
            _ratingWindow?.Dispose();
            _ratingWindow = null;
        };
        _ratingWindow.OpenCentered();
    }
}
