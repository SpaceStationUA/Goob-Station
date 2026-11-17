// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Crayon;
using Content.Shared.Administration;
using Content.Shared.Eui;
using Robust.Shared.Network;

namespace Content.Server._Pirate.Crayon;

public sealed class CrayonAdminEui(NetUserId? owner, string title, EntityUid? stamp) : BaseEui
{
    private readonly IAdminManager _admins = IoCManager.Resolve<IAdminManager>();
    private readonly CrayonLibrarySystem _library = IoCManager.Resolve<IEntityManager>().System<CrayonLibrarySystem>();
    private bool _placed = stamp != null;
    private int _page;
    private string? _status;

    public override void Opened()
    {
        _admins.OnPermsChanged += OnPermsChanged;
        StateDirty();
    }

    public override void Closed()
    {
        _admins.OnPermsChanged -= OnPermsChanged;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Admin))
            Close();
    }

    public override EuiStateBase GetNewState()
    {
        return _library.GetPage(owner, title, stamp, _placed, _page, _status);
    }

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown)
            return;
        if (!_admins.HasAdminFlag(Player, AdminFlags.Admin))
        {
            Close();
            return;
        }

        switch (msg)
        {
            case CrayonAdminPage page when stamp == null:
                _placed = page.Placed;
                _page = page.Page;
                _status = null;
                break;
            case CrayonAdminAction action:
                _status = _library.Moderate(Player, owner, stamp, action);
                break;
            default:
                return;
        }

        StateDirty();
    }
}
