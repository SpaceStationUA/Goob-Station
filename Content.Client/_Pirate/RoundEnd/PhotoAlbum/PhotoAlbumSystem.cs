// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Content.Shared.GameTicking;
using Content.Shared._Pirate.RoundEnd;

namespace Content.Client._Pirate.RoundEnd.PhotoAlbum;

public sealed class PhotoAlbumSystem : EntitySystem
{
    private static readonly TimeSpan ImageRequestTimeout = TimeSpan.FromSeconds(10);

    public IReadOnlyList<AlbumData>? Albums { get; private set; }
    public event Action? AlbumsUpdated;
    private readonly Dictionary<Guid, byte[]?> _fullImageData = new();
    private readonly Dictionary<Guid, PendingImageRequest> _pendingImageRequests = new();
    private readonly object _pendingImageRequestsLock = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<PhotoAlbumEvent>(OnStationImagesReceived);
        SubscribeNetworkEvent<PhotoAlbumImageResponseEvent>(OnPhotoImageReceived);
        SubscribeNetworkEvent<TickerJoinGameEvent>(OnJoinGame);
    }

    private void OnStationImagesReceived(PhotoAlbumEvent ev)
    {
        ClearImageCaches();
        Albums = ev.Albums?.ToList();
        AlbumsUpdated?.Invoke();
    }

    private void OnPhotoImageReceived(PhotoAlbumImageResponseEvent ev)
    {
        if (!TryTakePendingImageRequest(ev.ImageId, out var pending))
            return;

        _fullImageData[ev.ImageId] = ev.ImageData;
        pending.Dispose();
        TryCompletePendingRequest(pending.Completion, ev.ImageData);
    }

    private void OnJoinGame(TickerJoinGameEvent ev)
    {
        ResetAlbums();
    }

    public Task<byte[]?> GetFullImageDataAsync(Guid imageId)
    {
        if (_fullImageData.TryGetValue(imageId, out var imageData))
            return Task.FromResult(imageData);

        lock (_pendingImageRequestsLock)
        {
            if (_pendingImageRequests.TryGetValue(imageId, out var pending))
                return pending.Completion.Task;
        }

        var completion = new TaskCompletionSource<byte[]?>();
        var request = new PendingImageRequest(completion, ImageRequestTimeout, () => OnImageRequestTimedOut(imageId));

        lock (_pendingImageRequestsLock)
        {
            if (_pendingImageRequests.TryGetValue(imageId, out var existing))
            {
                request.Dispose();
                return existing.Completion.Task;
            }

            _pendingImageRequests[imageId] = request;
        }

        RaiseNetworkEvent(new PhotoAlbumImageRequestEvent(imageId));
        return completion.Task;
    }

    public void ResetAlbums()
    {
        Albums = null;
        ClearImageCaches();
        AlbumsUpdated?.Invoke();
    }

    private void ClearImageCaches()
    {
        List<PendingImageRequest> pendingRequests = new();

        lock (_pendingImageRequestsLock)
        {
            foreach (var request in _pendingImageRequests.Values)
            {
                pendingRequests.Add(request);
            }

            _pendingImageRequests.Clear();
        }

        foreach (var request in pendingRequests)
        {
            request.Dispose();
            TryCompletePendingRequest(request.Completion, null);
        }

        _fullImageData.Clear();
    }

    private void OnImageRequestTimedOut(Guid imageId)
    {
        if (!TryTakePendingImageRequest(imageId, out var request))
            return;

        request.Dispose();
        TryCompletePendingRequest(request.Completion, null);
    }

    private static void TryCompletePendingRequest(TaskCompletionSource<byte[]?> completion, byte[]? imageData)
    {
        _ = Task.Run(() => completion.TrySetResult(imageData));
    }

    private bool TryTakePendingImageRequest(Guid imageId, [NotNullWhen(true)] out PendingImageRequest? request)
    {
        lock (_pendingImageRequestsLock)
        {
            return _pendingImageRequests.Remove(imageId, out request);
        }
    }

    private sealed class PendingImageRequest : IDisposable
    {
        public TaskCompletionSource<byte[]?> Completion { get; }

        private readonly CancellationTokenSource _timeoutCancellation = new();
        private readonly CancellationTokenRegistration _timeoutRegistration;

        public PendingImageRequest(
            TaskCompletionSource<byte[]?> completion,
            TimeSpan timeout,
            Action timeoutAction)
        {
            Completion = completion;
            _timeoutCancellation.CancelAfter(timeout);
            _timeoutRegistration = _timeoutCancellation.Token.Register(timeoutAction);
        }

        public void Dispose()
        {
            _timeoutRegistration.Dispose();
            _timeoutCancellation.Dispose();
        }
    }
}


