// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Goobstation.Common.SecretSalt;

/// <summary>
/// The server has stopped accepting reviews for this completed SecretSalt round.
/// This closes the voluntary client dialog without requiring player interaction.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecretSaltFeedbackClosedEvent : EntityEventArgs
{
    public int RoundId { get; }

    public SecretSaltFeedbackClosedEvent(int roundId)
    {
        RoundId = roundId;
    }
}
