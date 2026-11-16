// SPDX-License-Identifier: MIT

using Lidgren.Network;

namespace Content.Shared._Pirate.Network;

// Pirate: backport the buffer checks used by Omu #2166 for our pinned RobustToolbox.
// Source: space-wizards/RobustToolbox@1cf884c24dbccd7df4ebb9148e0fc2d279f4f365.
public static class NetMessageBufferExtensions
{
    // Keep these unsigned so multiplying a received int count promotes to long.
    public const uint MinSizeOfString = 1;
    public const uint SizeOfNetEntity = sizeof(int);
    public const uint SizeOfTimeSpan = sizeof(long);

    public static void AssertAtLeastSize(this NetIncomingMessage message, long size)
    {
        var remainingBits = message.LengthBits - message.Position;
        if (size < 0 || size > remainingBits / 8)
        {
            throw new InvalidOperationException(
                $"Unable to read {size} bytes with only {remainingBits} bits left in the message.");
        }
    }
}
