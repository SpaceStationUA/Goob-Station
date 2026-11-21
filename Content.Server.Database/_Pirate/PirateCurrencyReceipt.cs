// SPDX-License-Identifier: AGPL-3.0-or-later

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Content.Server.Database;

public abstract partial class ServerDbContext
{
    public DbSet<PirateCurrencyReceipt> PirateCurrencyReceipts => Set<PirateCurrencyReceipt>();
}

[Table("pirate_currency_receipt")]
public sealed class PirateCurrencyReceipt
{
    [Key, MaxLength(36)] public string OperationId { get; set; } = default!;
    [Required, MaxLength(64)] public string RequestHash { get; set; } = default!;
    [Required, MaxLength(24)] public string State { get; set; } = default!;
    public int? Balance { get; set; }
}
