// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Timing;
using Robust.Shared.Graphics;

namespace Content.Client._Pirate.Photo;

/// <summary>
/// Overlay channels that can be suppressed during photo capture.
/// </summary>
[Flags]
public enum PhotoCaptureSuppressionMask
{
    None = 0,
    StatusIndicators = 1 << 0,
    VisionEffects = 1 << 1,
    AllPhotoOverlays = StatusIndicators | VisionEffects,
}

/// <summary>
/// Tracks temporary client-side render filters for photo capture.
/// </summary>
public sealed class PhotoCaptureFilterSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    private const double DefaultScopeTimeoutSeconds = 8.0;
    private int _nextScopeId;
    private readonly Dictionary<int, ScopeData> _activeScopes = new();

    public bool SuppressStatusIconsForPhotoCapture
    {
        get
        {
            CleanupExpiredScopes();

            foreach (var (_, scope) in _activeScopes)
            {
                if ((scope.Mask & PhotoCaptureSuppressionMask.StatusIndicators) != 0)
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Begins suppression for a specific eye (or globally when eye is null).
    /// Suppression expires automatically if the scope is not disposed in time.
    /// </summary>
    public IDisposable BeginSuppression(IEye? eye, PhotoCaptureSuppressionMask mask, TimeSpan? timeout = null)
    {
        CleanupExpiredScopes();

        if (mask == PhotoCaptureSuppressionMask.None)
            return new EmptyScope();

        var scopeId = _nextScopeId++;
        var expiresAt = _timing.RealTime + (timeout ?? TimeSpan.FromSeconds(DefaultScopeTimeoutSeconds));
        _activeScopes.Add(scopeId, new ScopeData(eye, mask, expiresAt));
        return new Scope(this, scopeId);
    }

    public IDisposable BeginPhotoCaptureSuppression(IEye? eye, TimeSpan? timeout = null)
        => BeginSuppression(eye, PhotoCaptureSuppressionMask.AllPhotoOverlays, timeout);

    // Legacy wrappers retained for compatibility with existing call sites.
    public IDisposable BeginSuppressStatusIcons(IEye? eye, TimeSpan? timeout = null)
        => BeginSuppression(eye, PhotoCaptureSuppressionMask.StatusIndicators, timeout);

    public IDisposable BeginSuppressStatusIcons()
    {
        return BeginSuppression(null, PhotoCaptureSuppressionMask.StatusIndicators);
    }

    public bool IsSuppressedForEye(IEye? eye, PhotoCaptureSuppressionMask mask)
    {
        if (mask == PhotoCaptureSuppressionMask.None)
            return false;

        CleanupExpiredScopes();

        foreach (var (_, scope) in _activeScopes)
        {
            if ((scope.Mask & mask) == 0)
                continue;

            // Null-eye scope means global suppression.
            if (scope.Eye == null || ReferenceEquals(scope.Eye, eye))
                return true;
        }

        return false;
    }

    public bool SuppressStatusIconsForPhotoCaptureForEye(IEye? eye)
        => IsSuppressedForEye(eye, PhotoCaptureSuppressionMask.StatusIndicators);

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        CleanupExpiredScopes();
    }

    private void EndSuppressMaskScope(int scopeId)
    {
        _activeScopes.Remove(scopeId);
    }

    private void CleanupExpiredScopes()
    {
        if (_activeScopes.Count == 0)
            return;

        var now = _timing.RealTime;
        List<int>? expired = null;

        foreach (var (scopeId, scope) in _activeScopes)
        {
            if (now < scope.ExpiresAt)
                continue;

            expired ??= new List<int>();
            expired.Add(scopeId);
        }

        if (expired == null)
            return;

        foreach (var scopeId in expired)
        {
            _activeScopes.Remove(scopeId);
        }
    }

    private sealed class Scope : IDisposable
    {
        private PhotoCaptureFilterSystem? _owner;
        private int _scopeId;

        public Scope(PhotoCaptureFilterSystem? owner, int scopeId)
        {
            _owner = owner;
            _scopeId = scopeId;
        }

        public void Dispose()
        {
            if (_owner == null)
                return;

            _owner.EndSuppressMaskScope(_scopeId);
            _owner = null;
            _scopeId = -1;
        }
    }

    private sealed class EmptyScope : IDisposable
    {
        public void Dispose()
        {
        }
    }

    private readonly record struct ScopeData(IEye? Eye, PhotoCaptureSuppressionMask Mask, TimeSpan ExpiresAt);
}


