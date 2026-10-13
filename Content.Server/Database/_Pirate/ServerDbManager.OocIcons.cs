// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using System.Threading.Tasks;

namespace Content.Server.Database;

public partial interface IServerDbManager
{
    Task<string?> GetPlayerOocIcon(Guid player, CancellationToken cancel);
}

public sealed partial class ServerDbManager
{
    public Task<string?> GetPlayerOocIcon(Guid player, CancellationToken cancel)
    {
        DbReadOpsMetric.Inc();
        return RunDbCommand(() => _db.GetPlayerOocIcon(player, cancel));
    }
}
