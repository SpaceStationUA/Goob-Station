using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Pirate.Client.Administration.UI.Bwoink;

public sealed class AHelpRatingWindow : DefaultWindow
{
    public event Action<byte>? RatingSelected;

    public AHelpRatingWindow(byte currentRating, string adminName)
    {
        Title = Loc.GetString("bwoink-ticket-rating-window-title");
        MinSize = new Vector2(420, 150);

        var root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
            Margin = new Thickness(8),
        };

        var prompt = new RichTextLabel();
        prompt.SetMessage(Loc.GetString("bwoink-ticket-rating-window-prompt", ("admin", adminName)));
        root.AddChild(prompt);

        var current = new RichTextLabel();
        current.SetMessage(currentRating == 0
            ? Loc.GetString("bwoink-ticket-rating-window-current-none")
            : Loc.GetString("bwoink-ticket-rating-window-current", ("rating", currentRating)));
        root.AddChild(current);

        var ratingsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 6,
        };

        for (byte rating = 1; rating <= 5; rating++)
        {
            var selected = rating;
            var isCurrent = currentRating == rating;
            var button = new Button
            {
                Text = isCurrent ? $"{rating}/5 *" : $"{rating}/5",
                HorizontalExpand = true,
            };
            button.OnPressed += _ => RatingSelected?.Invoke(selected);
            ratingsContainer.AddChild(button);
        }

        root.AddChild(ratingsContainer);
        Contents.AddChild(root);
    }
}
