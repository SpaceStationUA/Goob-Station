// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Shared._Pirate.Network;
using Content.Shared.Chat;
using Content.Shared.Eui;
using Content.Shared.Preferences;
using Lidgren.Network;
using NUnit.Framework;

namespace Content.Tests.Shared._Pirate.Network;

[TestFixture]
public sealed class NetMessageBufferValidationTest
{
    [TestCase(-1)]
    [TestCase(8)]
    public void CharacterUpdatesRejectMissingPayload(int claimedLength)
    {
        var buffer = CreateBuffer();
        buffer.Write(0); // Character slot.
        buffer.WriteVariableInt32(claimedLength);

        Assert.Throws<InvalidOperationException>(() =>
            new MsgUpdateCharacter().ReadFromBuffer(buffer, null!));
    }

    [TestCase(-1)]
    [TestCase(8)]
    public void EuiMessagesRejectMissingPayload(int claimedLength)
    {
        var buffer = CreateBuffer();
        buffer.Write(0u); // EUI ID.
        buffer.WriteVariableInt32(claimedLength);

        Assert.Throws<InvalidOperationException>(() =>
            new MsgEuiMessage().ReadFromBuffer(buffer, null!));
    }

    [TestCase(-1)]
    [TestCase(8)]
    public void ChatDeletionRejectsMissingEntities(int claimedCount)
    {
        var buffer = CreateBuffer();
        buffer.Write(0); // Deletion key.
        buffer.Write(claimedCount);

        Assert.Throws<InvalidOperationException>(() =>
            new MsgDeleteChatMessagesBy().ReadFromBuffer(buffer, null!));
    }

    [Test]
    public void LargeEntityCountCannotWrapToAnEmptyPayload()
    {
        var buffer = CreateBuffer();
        const int count = 1 << 30;
        var requiredSize = count * NetMessageBufferExtensions.SizeOfNetEntity;
        Assert.That(requiredSize, Is.EqualTo(1L << 32));
        Assert.Throws<InvalidOperationException>(() => buffer.AssertAtLeastSize(requiredSize));
    }

    [Test]
    public void ChecksUseRemainingPayloadBits()
    {
        var buffer = CreateBuffer();
        buffer.Write((byte) 42);
        buffer.Write((byte) 43);
        buffer.ReadBoolean();

        Assert.DoesNotThrow(() => buffer.AssertAtLeastSize(1));
        Assert.Throws<InvalidOperationException>(() => buffer.AssertAtLeastSize(2));
        buffer.ReadPadBits();
        buffer.ReadByte();
        Assert.DoesNotThrow(() => buffer.AssertAtLeastSize(0));
        Assert.Throws<InvalidOperationException>(() => buffer.AssertAtLeastSize(1));
    }

    private static NetIncomingMessage CreateBuffer()
    {
        // Lidgren exposes only internal constructors; no peer or network traffic is needed.
        var buffer = (NetIncomingMessage) Activator.CreateInstance(typeof(NetIncomingMessage), nonPublic: true)!;
        buffer.Data = new byte[32];
        return buffer;
    }
}
