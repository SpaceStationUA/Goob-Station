// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.EUI;
using Content.Shared._Pirate.Revolutionary;
using Content.Shared.Eui;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Revolutionary;

public sealed class RevolutionaryRecruitmentEui(Action<ICommonSession> accept, Action closed) : BaseEui
{
    public override void HandleMessage(EuiMessageBase msg)
    {
        if (msg is AcceptRevolutionaryRecruitmentMessage)
        {
            accept(Player);
            Close();
            return;
        }

        base.HandleMessage(msg);
    }

    public override void Closed() => closed();
}
