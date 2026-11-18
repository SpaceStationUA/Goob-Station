// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Eui;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Crayon;

[Serializable, NetSerializable]
public sealed record CrayonAdminRow(Guid? Saved, NetEntity? Stamp, string Name, CrayonSymbol? Symbol);

[Serializable, NetSerializable]
public sealed class CrayonAdminEuiState(string title, bool placed, bool single, int page, int pages,
    List<CrayonAdminRow> rows, string? status) : EuiStateBase
{
    public readonly string Title = title;
    public readonly bool Placed = placed;
    public readonly bool Single = single;
    public readonly int Page = page;
    public readonly int Pages = pages;
    public readonly List<CrayonAdminRow> Rows = rows;
    public readonly string? Status = status;
}

[Serializable, NetSerializable]
public sealed class CrayonAdminPage(bool placed, int page) : EuiMessageBase
{
    public readonly bool Placed = placed;
    public readonly int Page = page;
}

[Serializable, NetSerializable]
public sealed class CrayonAdminAction(Guid? saved, NetEntity? stamp, bool delete) : EuiMessageBase
{
    public readonly Guid? Saved = saved;
    public readonly NetEntity? Stamp = stamp;
    public readonly bool Delete = delete;
}
