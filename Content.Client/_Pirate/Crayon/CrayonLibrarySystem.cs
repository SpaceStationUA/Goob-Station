// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Shared._Pirate.Crayon;
using Robust.Shared.Network;

namespace Content.Client._Pirate.Crayon;

public sealed class CrayonLibrarySystem : EntitySystem
{
    [Dependency] private readonly IClientNetManager _net = default!;

    public List<CrayonDrawing> Drawings { get; private set; } = new();
    public string? Error { get; private set; }
    public event Action? Changed;

    private int _nextRequest;
    private readonly Dictionary<int, TaskCompletionSource<CrayonSaveResult>> _pending = new();

    public override void Initialize()
    {
        SubscribeNetworkEvent<CrayonLibraryState>(OnLibrary);
        SubscribeNetworkEvent<CrayonSaveResult>(OnSaved);
        _net.Disconnect += OnDisconnect;
    }

    public override void Shutdown()
    {
        _net.Disconnect -= OnDisconnect;
        Reset();
        base.Shutdown();
    }

    private void OnLibrary(CrayonLibraryState state)
    {
        if (state.Drawings != null)
            Drawings = state.Drawings;
        Error = state.Error;
        Changed?.Invoke();
    }

    private void OnSaved(CrayonSaveResult result)
    {
        if (_pending.Remove(result.Request, out var request))
            request.TrySetResult(result);
    }

    public void Request() => RaiseNetworkEvent(new CrayonLibraryRequest());

    public void Delete(Guid id) => RaiseNetworkEvent(new CrayonDeleteDrawing(id));

    public Task<CrayonSaveResult> Save(Guid? previous, string name, CrayonSymbol symbol)
    {
        var id = ++_nextRequest;
        var completion = new TaskCompletionSource<CrayonSaveResult>();
        _pending.Add(id, completion);
        RaiseNetworkEvent(new CrayonSaveDrawing(id, previous, name, symbol));
        return completion.Task;
    }

    private void OnDisconnect(object? sender, NetDisconnectedArgs args) => Reset();

    private void Reset()
    {
        Drawings = new();
        Error = null;
        foreach (var (id, request) in _pending)
            request.TrySetResult(new CrayonSaveResult(id, null, "pirate-crayon-library-unavailable"));
        _pending.Clear();
    }
}
