// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Goobstation.Common.ServerCurrency;
using Content.Server._Pirate.ServerCurrency; // Pirate
using Content.Server.Database;
using Robust.Server.Player;
using Robust.Shared.Asynchronous;
using Robust.Shared.Network;

namespace Content.Goobstation.Server.ServerCurrency
{
    public sealed partial class ServerCurrencyManager : ICommonCurrencyManager
    {
        [Dependency] private IServerDbManager _db = default!;
        [Dependency] private ITaskManager _task = default!;
        [Dependency] private IPlayerManager _player = default!;
        private readonly List<Task> _pendingSaveTasks = new();

        public event Action? ClientBalanceChange;
        public event Action<PlayerBalanceChangeEvent>? BalanceChange;
        private ISawmill _sawmill = default!;

        public void Initialize()
        {
            _sawmill = Logger.GetSawmill("server_currency");
        }

        /// <inheritdoc/>
        public void Shutdown()
        {
            Task pending;
            lock (_pendingSaveTasks)
                pending = Task.WhenAll(_pendingSaveTasks);
            _task.BlockWaitOnTask(pending);
        }

        /// <inheritdoc/>
        public bool CanAfford(NetUserId? userId, int amount, out int balance)
        {
            balance = GetBalance(userId);
            return balance >= amount && balance - amount >= 0;
        }

        /// <inheritdoc/>
        public string Stringify(int amount) => amount == 1
            ? $"{amount} {Loc.GetString("server-currency-name-singular")}"
            : $"{amount} {Loc.GetString("server-currency-name-plural")}";

        /// <inheritdoc/>
        public int AddCurrency(NetUserId userId, int amount)
        {
            var newBalance = Adjust(userId, amount, PirateCurrencyAdjustment.Add).New; // Pirate
            _sawmill.Info($"Added {amount} currency to {userId} account. Current balance: {newBalance}");
            return newBalance;
        }

        /// <inheritdoc/>
        public int RemoveCurrency(NetUserId userId, int amount)
        {
            var newBalance = Adjust(userId, checked(-amount), PirateCurrencyAdjustment.Add).New; // Pirate
            _sawmill.Info($"Removed {amount} currency from {userId} account. Current balance: {newBalance}");
            return newBalance;
        }

        /// <inheritdoc/>
        public (int, int) TransferCurrency(NetUserId sourceUserId, NetUserId targetUserId, int amount)
        {
            // Pirate: debit and credit commit together, including for legacy callers.
            var result = Apply(new(Guid.NewGuid(), sourceUserId, targetUserId, amount, "transfer"));
            if (result.State != "applied")
                throw new InvalidOperationException($"Currency transfer failed: {result.State}");
            return (result.Balance!.Value, result.TargetBalance!.Value);
        }

        /// <inheritdoc/>
        public int SetBalance(NetUserId userId, int amount)
        {
            var oldBalance = Adjust(userId, amount, PirateCurrencyAdjustment.Set).Old; // Pirate
            _sawmill.Info($"Setting {userId} account balance to {amount} from {oldBalance}");
            return oldBalance;
        }

        /// <inheritdoc/>
        public int GetBalance(NetUserId? userId = null)
        {
            return userId == null ? 0 : Task.Run(() => _db.GetServerCurrency(userId.Value)).GetAwaiter().GetResult();
        }

        #region Internal/Async tasks

        /// <summary>
        /// Track a database save task to make sure we block server shutdown on it.
        /// </summary>
        private async Task TrackPendingAsync(Task task)
        {
            lock (_pendingSaveTasks)
                _pendingSaveTasks.Add(task);

            // Pirate: the caller awaits the save and receives its exception. Only track completion here.
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            lock (_pendingSaveTasks)
                _pendingSaveTasks.Remove(task);
        }

        #endregion
    }
}
