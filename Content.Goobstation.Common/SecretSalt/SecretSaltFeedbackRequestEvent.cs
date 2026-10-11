// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Goobstation.Common.SecretSalt;

/// <summary>
/// Sent by the client only after a round has ended. This contains no sender
/// account identifier; the server must use the authenticated network session.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecretSaltFeedbackRequestEvent : EntityEventArgs
{
    public int RoundId { get; }
    public int Rating { get; }
    public SecretSaltFeedbackReason[] Reasons { get; }
    public string? Comment { get; }

    public SecretSaltFeedbackRequestEvent(
        int roundId,
        int rating,
        SecretSaltFeedbackReason[] reasons,
        string? comment)
    {
        RoundId = roundId;
        Rating = rating;
        Reasons = reasons;
        Comment = comment;
    }
}
