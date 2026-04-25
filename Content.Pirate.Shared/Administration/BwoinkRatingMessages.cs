using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Administration;

[Serializable, NetSerializable]
public sealed class BwoinkAHelpWindowClosed : EntityEventArgs
{
    public NetUserId Channel { get; }

    public BwoinkAHelpWindowClosed(NetUserId channel)
    {
        Channel = channel;
    }
}

[Serializable, NetSerializable]
public sealed class BwoinkTicketRatingPrompt : EntityEventArgs
{
    /// <summary>
    /// Current saved rating (0 means no rating chosen yet).
    /// </summary>
    public byte CurrentRating { get; }

    /// <summary>
    /// Name of the admin being rated.
    /// </summary>
    public string AdminName { get; }

    public BwoinkTicketRatingPrompt(byte currentRating, string adminName)
    {
        CurrentRating = currentRating;
        AdminName = adminName;
    }
}

[Serializable, NetSerializable]
public sealed class BwoinkTicketRatingMessage : EntityEventArgs
{
    public byte Rating { get; }

    public BwoinkTicketRatingMessage(byte rating)
    {
        Rating = rating;
    }
}
