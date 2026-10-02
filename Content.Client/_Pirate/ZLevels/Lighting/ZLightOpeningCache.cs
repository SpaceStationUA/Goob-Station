// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.Map;

namespace Content.Client._Pirate.ZLevels.Lighting;

/// <summary>
/// Short-lived cosmetic geometry results. Photometric values are deliberately not cached. Positions are in
/// the source light's frame (<see cref="Key.Frame"/>, its grid), so a rigid moving grid keeps its entries.
/// </summary>
internal sealed class ZLightOpeningCache
{
    internal readonly record struct Key(EntityUid Source, MapId SourceMap, EntityUid OpeningMap, EntityUid Frame);

    internal sealed class Entry
    {
        public Vector2 Position;
        public float Radius;
        public TimeSpan SampledAt;
        public readonly List<(Vector2 Center, float Distance)> Openings = new();
    }

    private readonly Dictionary<Key, Entry> _entries = new();
    private readonly List<Key> _expired = new();
    private TimeSpan _nextPrune;

    public Entry Get(Key key, Vector2 position, float radius, TimeSpan now, float lifetime, out bool refresh)
    {
        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new Entry();
            _entries.Add(key, entry);
            refresh = true;
        }
        else
        {
            // Zero disables reuse. An exact source transform/radius change always updates now.
            refresh = !float.IsFinite(lifetime) || lifetime <= 0 || now < entry.SampledAt ||
                      (now - entry.SampledAt).TotalSeconds >= lifetime ||
                      position != entry.Position || radius != entry.Radius;
        }

        if (refresh)
        {
            entry.Position = position;
            entry.Radius = radius;
            entry.SampledAt = now;
            entry.Openings.Clear();
        }

        return entry;
    }

    public void Prune(TimeSpan now)
    {
        if (now < _nextPrune)
            return;

        _nextPrune = now + TimeSpan.FromSeconds(1);
        _expired.Clear();
        foreach (var (key, entry) in _entries)
        {
            if (now - entry.SampledAt >= TimeSpan.FromSeconds(1))
                _expired.Add(key);
        }

        foreach (var key in _expired)
            _entries.Remove(key);
    }

    public void InvalidateMap(MapId mapId, EntityUid mapUid)
    {
        _expired.Clear();
        foreach (var key in _entries.Keys)
        {
            if (key.SourceMap == mapId || key.OpeningMap == mapUid)
                _expired.Add(key);
        }

        foreach (var key in _expired)
            _entries.Remove(key);
    }

    public void Clear()
    {
        _entries.Clear();
        _expired.Clear();
        _nextPrune = TimeSpan.Zero;
    }
}
