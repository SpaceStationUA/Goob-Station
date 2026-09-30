// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Numerics;
using Content.Client._Pirate.ZLevels.Lighting;
using NUnit.Framework;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZLightOpeningCacheTest
{
    private static readonly ZLightOpeningCache.Key Key = new(new EntityUid(1), new MapId(1), new EntityUid(2));

    [Test]
    public void StableSourceReusesGeometryOnlyUntilExpiry()
    {
        var cache = new ZLightOpeningCache();
        var entry = cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0.1f, out var refresh);
        Assert.That(refresh, Is.True);
        entry.Openings.Add((Vector2.One, 1));

        var reused = cache.Get(Key, Vector2.Zero, 4, TimeSpan.FromMilliseconds(50), 0.1f, out refresh);
        Assert.That(refresh, Is.False);
        Assert.That(reused.Openings.Count, Is.EqualTo(1));

        cache.Get(Key, Vector2.Zero, 4, TimeSpan.FromMilliseconds(101), 0.1f, out refresh);
        Assert.That(refresh, Is.True);
        Assert.That(entry.Openings, Is.Empty);
    }

    [Test]
    public void MovementRadiusAndMapChangesCannotReusePreviousGeometry()
    {
        var cache = new ZLightOpeningCache();
        cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0.1f, out _);
        cache.Get(Key, Vector2.One, 4, TimeSpan.Zero, 0.1f, out var moved);
        cache.Get(Key, Vector2.One, 5, TimeSpan.Zero, 0.1f, out var resized);
        cache.Get(Key with { SourceMap = new MapId(3) }, Vector2.One, 5, TimeSpan.Zero, 0.1f, out var remapped);
        cache.Get(Key with { OpeningMap = new EntityUid(4) }, Vector2.One, 5, TimeSpan.Zero, 0.1f, out var openingChanged);
        Assert.That(moved && resized && remapped && openingChanged, Is.True);
    }

    [Test]
    public void InvalidationAndDisabledCacheRefreshImmediately()
    {
        var cache = new ZLightOpeningCache();
        var original = cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0.1f, out _);
        original.Openings.Add((Vector2.One, 1));
        cache.Clear();
        var fresh = cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0.1f, out var refresh);
        Assert.That(refresh, Is.True);
        Assert.That(fresh.Openings, Is.Empty);
        cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0, out refresh);
        Assert.That(refresh, Is.True);
    }

    [Test]
    public void PruneReleasesSourcesNoLongerQueried()
    {
        var cache = new ZLightOpeningCache();
        var original = cache.Get(Key, Vector2.Zero, 4, TimeSpan.Zero, 0.1f, out _);
        cache.Prune(TimeSpan.FromSeconds(2));
        var replacement = cache.Get(Key, Vector2.Zero, 4, TimeSpan.FromSeconds(2), 0.1f, out var refresh);
        Assert.That(refresh, Is.True);
        Assert.That(replacement, Is.Not.SameAs(original));
    }
}
