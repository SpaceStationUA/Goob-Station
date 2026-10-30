// SPDX-License-Identifier: AGPL-3.0-or-later

using Lidgren.Network;

namespace Content.Shared.Voting;

public sealed partial class MsgVoteData
{
    public ushort[] PirateSubtractions = [];

    private void ReadPirateSubtractions(NetIncomingMessage buffer)
    {
        PirateSubtractions = new ushort[Options.Length];
        for (var i = 0; i < PirateSubtractions.Length; i++)
            PirateSubtractions[i] = buffer.ReadUInt16();
    }

    private void WritePirateSubtractions(NetOutgoingMessage buffer)
    {
        for (var i = 0; i < Options.Length; i++)
            buffer.Write(i < PirateSubtractions.Length ? PirateSubtractions[i] : (ushort) 0);
    }
}
