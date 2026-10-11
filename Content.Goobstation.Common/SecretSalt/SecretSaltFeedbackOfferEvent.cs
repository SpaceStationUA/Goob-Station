// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Goobstation.Common.SecretSalt;

/// <summary>
/// Server notification that this specific completed SecretSalt round may be
/// reviewed. No antagonist information or private feedback is included.
/// </summary>
[Serializable, NetSerializable]
public sealed class SecretSaltFeedbackOfferEvent : EntityEventArgs
{
    public int RoundId { get; }

    public SecretSaltFeedbackOfferEvent(int roundId)
    {
        RoundId = roundId;
    }
}
