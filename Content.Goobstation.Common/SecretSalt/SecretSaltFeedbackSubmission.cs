// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;

namespace Content.Goobstation.Common.SecretSalt;

/// <summary>
/// Available reasons for a voluntary, post-round Secret Salt review.
/// </summary>
public enum SecretSaltFeedbackReason : byte
{
    TooFewAntagonists = 1,
    TooManyAntagonists = 2,
    TooFewEvents = 3,
    TooChaotic = 4,
    Other = 5
}

/// <summary>
/// Five UI choices are encoded as a single byte on the network. An untrusted
/// client can therefore never request allocation of an arbitrarily long
/// reasons array during network deserialization.
/// </summary>
public static class SecretSaltFeedbackReasonMask
{
    private const byte ValidBits = 0b0001_1111;

    public static byte Encode(IReadOnlyList<SecretSaltFeedbackReason> reasons)
    {
        byte mask = 0;
        foreach (var reason in reasons)
        {
            var number = (int) reason;
            if (number is < 1 or > 5)
                throw new ArgumentOutOfRangeException(nameof(reasons), "Unknown feedback reason.");

            var bit = (byte) (1 << (number - 1));
            if ((mask & bit) != 0)
                throw new ArgumentException("Duplicate feedback reason.", nameof(reasons));

            mask |= bit;
        }

        return mask;
    }

    public static bool TryDecode(byte mask, out SecretSaltFeedbackReason[] reasons)
    {
        reasons = Array.Empty<SecretSaltFeedbackReason>();
        if ((mask & ~ValidBits) != 0)
            return false;

        // No more than five elements are ever constructed.
        var selected = new List<SecretSaltFeedbackReason>(5);
        for (var index = 0; index < 5; index++)
        {
            if ((mask & (1 << index)) != 0)
                selected.Add((SecretSaltFeedbackReason) (index + 1));
        }

        reasons = selected.ToArray();
        return true;
    }
}

/// <summary>
/// Player-supplied data only. Account identity is taken from the authenticated
/// server session, never from the submission.
/// </summary>
public sealed class SecretSaltFeedbackSubmission
{
    public int RoundId { get; init; }
    public int Rating { get; init; }
    public IReadOnlyList<SecretSaltFeedbackReason> Reasons { get; init; } =
        Array.Empty<SecretSaltFeedbackReason>();
    public string? Comment { get; init; }
}

/// <summary>
/// Server-side validation rules, deliberately independent from the review UI.
/// Comments are not substantively censored; presentation code must escape them
/// before sending to Discord.
/// </summary>
public static class SecretSaltFeedbackValidator
{
    public const int MaxCommentLength = 500;

    public static bool IsValid(SecretSaltFeedbackSubmission submission)
    {
        if (submission.RoundId <= 0 || submission.Rating is < 1 or > 5)
            return false;

        if (submission.Comment != null && submission.Comment.Length > MaxCommentLength)
            return false;

        var seen = new HashSet<SecretSaltFeedbackReason>();
        foreach (var reason in submission.Reasons)
        {
            if (!Enum.IsDefined(typeof(SecretSaltFeedbackReason), reason) || !seen.Add(reason))
                return false;
        }

        return true;
    }
}
