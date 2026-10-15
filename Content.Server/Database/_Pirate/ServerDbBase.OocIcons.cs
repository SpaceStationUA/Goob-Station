// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbBase
{
    public async Task<string?> GetPlayerOocIcon(Guid player, CancellationToken cancel)
    {
        await using var db = await GetDb(cancel);
        return await db.DbContext.Player
            .Where(p => p.UserId == player)
            .Select(p => p.OocIcon)
            .FirstOrDefaultAsync(cancel);
    }
}
