// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration;
using Content.Server.Administration.Managers;
using Content.Server.EUI;
using Content.Shared._Pirate.Revolution;
using Content.Shared.Administration;
using Content.Shared.Eui;

namespace Content.Server._Pirate.Revolution;

public sealed class PirateRevolutionOrderEui : BaseEui
{
    private readonly IAdminManager _admins = IoCManager.Resolve<IAdminManager>();
    private readonly PirateRevolutionOrdersSystem _orders = IoCManager.Resolve<IEntityManager>().System<PirateRevolutionOrdersSystem>();
    private List<RevOrderOption> _options = new();
    private PirateRevolutionOrdersSystem.OrderDraft? _draft;
    private string? _status;
    private int _revision;
    private int _round;
    private int _historyPage;
    private int _activityRound;
    private bool _activityRequested;

    public override void Opened()
    {
        _admins.OnPermsChanged += OnPermsChanged;
        _orders.OrderActivityChanged += SendActivity;
        Refresh();
        StateDirty();
    }

    public override void Closed()
    {
        _admins.OnPermsChanged -= OnPermsChanged;
        _orders.OrderActivityChanged -= SendActivity;
    }

    private void OnPermsChanged(AdminPermsChangedEventArgs args)
    {
        if (args.Player == Player && !_admins.HasAdminFlag(Player, AdminFlags.Fun))
            Close();
    }

    public override EuiStateBase GetNewState() =>
        new PirateRevolutionOrderEuiState(_revision, _options, _draft?.Preview, _status);

    public override void HandleMessage(EuiMessageBase msg)
    {
        base.HandleMessage(msg);
        if (IsShutDown)
            return;

        if (!_admins.HasAdminFlag(Player, AdminFlags.Fun))
        {
            Close();
            return;
        }

        if (msg is RevOrderReadHistory read)
        {
            _activityRequested = true;
            _activityRound = _orders.OrderRound;
            _historyPage = read.Page;
            SendActivity();
            return;
        }

        if (msg is RevOrderSetAutomatic automatic)
        {
            if (!_orders.SetAutomaticOrders(automatic.Round, automatic.Enabled))
                SendActivity();
            return;
        }

        var revision = msg switch
        {
            RevOrderPrepare prepare => prepare.Revision,
            RevOrderApply apply => apply.Revision,
            RevOrderRefresh refresh => refresh.Revision,
            _ => -1,
        };
        if (revision != _revision)
            return;

        _revision++;
        _status = null;
        if (_round != _orders.OrderRound)
        {
            Refresh("pirate-rev-ui-new-round");
        }
        else
        {
            switch (msg)
            {
                case RevOrderRefresh:
                    Refresh();
                    break;
                case RevOrderPrepare prepare:
                    Prepare(prepare);
                    break;
                case RevOrderApply apply:
                    Apply(apply);
                    break;
            }
        }

        StateDirty();
    }

    private void Refresh(string? status = null)
    {
        _round = _orders.OrderRound;
        _options = _orders.GetOrderOptions();
        _draft = null;
        _status = status == null ? null : Loc.GetString(status);
    }

    private void Prepare(RevOrderPrepare prepare)
    {
        if (prepare.Option < 0 || prepare.Option >= _options.Count || prepare.Crew == null)
        {
            Refresh("pirate-rev-ui-stale");
            return;
        }

        var option = _options[prepare.Option];
        var group = prepare.Group >= 0 && prepare.Group < option.Groups.Count ? option.Groups[prepare.Group] : null;
        if (group == null && (prepare.Group != -1 || option.Groups.Count != 0))
        {
            Refresh("pirate-rev-ui-stale");
            return;
        }

        _draft = _orders.PrepareOrder(option, group, prepare.Amount, prepare.Crew, prepare.Fine);
        if (_draft == null)
            Refresh("pirate-rev-ui-stale");
    }

    private void Apply(RevOrderApply apply)
    {
        if (_draft is not { } draft)
        {
            Refresh("pirate-rev-ui-stale");
            return;
        }

        var message = RevOrderAnnouncement.Render(apply.Template ?? string.Empty, draft.Preview.Values, out _status);
        if (message == null)
            return;

        _draft = null;
        Refresh(_orders.TryApplyOrder(draft, message, Player) ? null : "pirate-rev-ui-stale");
    }

    private void SendActivity()
    {
        if (!_activityRequested)
            return;

        if (_activityRound != _orders.OrderRound)
        {
            _activityRound = _orders.OrderRound;
            _historyPage = 0;
        }

        var activity = _orders.GetOrderActivity(_historyPage);
        _historyPage = activity.Page;
        SendMessage(activity);
    }
}
