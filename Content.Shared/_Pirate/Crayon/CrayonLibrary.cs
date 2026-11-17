// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Crayon;

[Serializable, NetSerializable]
public sealed record CrayonDrawing(Guid Id, string Name, CrayonSymbol Symbol)
{
    public const int Limit = 10;
    public const int MaxNameLength = 64;
}

[Serializable, NetSerializable]
public sealed class CrayonLibraryRequest : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class CrayonLibraryState(List<CrayonDrawing>? drawings, string? error = null) : EntityEventArgs
{
    public readonly List<CrayonDrawing>? Drawings = drawings;
    public readonly string? Error = error;
}

[Serializable, NetSerializable]
public sealed class CrayonSaveDrawing(int request, Guid? previous, string name, CrayonSymbol symbol) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly Guid? Previous = previous;
    public readonly string Name = name;
    public readonly CrayonSymbol Symbol = symbol;
}

[Serializable, NetSerializable]
public sealed class CrayonSaveResult(int request, Guid? saved, string? error) : EntityEventArgs
{
    public readonly int Request = request;
    public readonly Guid? Saved = saved;
    public readonly string? Error = error;
}

[Serializable, NetSerializable]
public sealed class CrayonDeleteDrawing(Guid id) : EntityEventArgs
{
    public readonly Guid Id = id;
}
