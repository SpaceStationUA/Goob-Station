// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using Content.Shared._Pirate.CustomMarkings;
using NUnit.Framework;

namespace Content.Tests._Pirate.CustomMarkings;

/// <summary>Hash and name checks, what survives of a worn list, and its saved form.</summary>
[TestFixture]
[TestOf(typeof(CustomMarkingRules))]
public sealed class CustomMarkingRulesTest
{
    private static readonly string HashA = new('a', CustomMarkingRules.HashLength);
    private static readonly string HashB = new('b', CustomMarkingRules.HashLength);

    [Test]
    public void HashValidityTest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.IsValidHash(HashA), Is.True);
            Assert.That(CustomMarkingRules.IsValidHash("0123456789abcdef" + new string('0', 48)), Is.True);
            Assert.That(CustomMarkingRules.IsValidHash(null), Is.False);
            Assert.That(CustomMarkingRules.IsValidHash(string.Empty), Is.False);
            Assert.That(CustomMarkingRules.IsValidHash(HashA[1..]), Is.False, "too short");
            Assert.That(CustomMarkingRules.IsValidHash(HashA + "a"), Is.False, "too long");
            Assert.That(CustomMarkingRules.IsValidHash(new string('A', CustomMarkingRules.HashLength)), Is.False, "upper case");
            Assert.That(CustomMarkingRules.IsValidHash(new string('g', CustomMarkingRules.HashLength)), Is.False, "not hex");
            Assert.That(CustomMarkingRules.IsValidHash("../" + HashA[3..]), Is.False, "a path");
        });
    }

    [Test]
    public void CleanNameTest()
    {
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.CleanName(null), Is.Empty);
            Assert.That(CustomMarkingRules.CleanName("   "), Is.Empty);
            Assert.That(CustomMarkingRules.CleanName("  Left arm scar  "), Is.EqualTo("Left arm scar"));
            Assert.That(CustomMarkingRules.CleanName("two\nlines\t"), Is.EqualTo("twolines"));
            Assert.That(CustomMarkingRules.CleanName(new string('x', 200)), Has.Length.EqualTo(CustomMarkingRules.MaxNameLength));
        });
    }

    [Test]
    public void CleanWornTest()
    {
        var worn = new List<CustomMarking>
        {
            new(HashA, CustomMarkingPlacement.Skin),
            new(HashA, CustomMarkingPlacement.Skin),
            new("nope", CustomMarkingPlacement.Skin),
            default,
            new(HashA, (CustomMarkingPlacement) 200),
            new(HashA, CustomMarkingPlacement.Front),
            new(HashB, CustomMarkingPlacement.Hair),
        };

        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.Clean(null, 4), Is.Empty);
            Assert.That(CustomMarkingRules.Clean(worn, 4), Is.EqualTo(new[]
            {
                new CustomMarking(HashA, CustomMarkingPlacement.Skin),
                new CustomMarking(HashA, CustomMarkingPlacement.Front),
                new CustomMarking(HashB, CustomMarkingPlacement.Hair),
            }), "bad and repeated entries go, order stays");
            Assert.That(CustomMarkingRules.Clean(worn, 2), Has.Count.EqualTo(2));
            Assert.That(CustomMarkingRules.Clean(worn, 0), Is.Empty);
        });

        var many = Enumerable.Range(0, 40).Select(i => new CustomMarking(i.ToString("x64"), CustomMarkingPlacement.Skin));
        Assert.That(CustomMarkingRules.Clean(many, 1000), Has.Count.EqualTo(CustomMarkingRules.MaxWornCap), "the setting can't lift the cap");
    }

    [Test]
    public void StoredFormTest()
    {
        var worn = new List<CustomMarking>
        {
            new(HashB, CustomMarkingPlacement.Behind),
            new(HashA, CustomMarkingPlacement.Front),
        };

        var stored = CustomMarkingRules.ToStored(worn);
        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.FromStored(stored), Is.EqualTo(worn));
            Assert.That(CustomMarkingRules.ToStored(new List<CustomMarking>()), Is.Empty);
            Assert.That(CustomMarkingRules.FromStored(null), Is.Empty);
            Assert.That(CustomMarkingRules.FromStored(string.Empty), Is.Empty);
            Assert.That(CustomMarkingRules.FromStored($"junk,{HashA},{HashA}:x,{HashA}:99,:1,{HashA}:1"),
                Is.EqualTo(new[] { new CustomMarking(HashA, CustomMarkingPlacement.Skin) }), "unreadable pairs are dropped");
        });
    }

    [Test]
    public void ToggleTest()
    {
        var a = new CustomMarking(HashA, CustomMarkingPlacement.Skin);
        var b = new CustomMarking(HashB, CustomMarkingPlacement.Skin);
        var c = new CustomMarking(HashA, CustomMarkingPlacement.Hair);
        var worn = new List<CustomMarking>();

        Assert.That(CustomMarkingRules.Toggle(worn, a, 2), Is.True);
        Assert.That(CustomMarkingRules.Toggle(worn, b, 2), Is.True);
        Assert.That(worn, Is.EqualTo(new[] { a, b }));

        Assert.That(CustomMarkingRules.Toggle(worn, c, 2), Is.False, "no room for a third");
        Assert.That(worn, Is.EqualTo(new[] { a, b }));

        Assert.That(CustomMarkingRules.Toggle(worn, a, 2), Is.True, "a worn one comes off, even when full");
        Assert.That(worn, Is.EqualTo(new[] { b }));
        Assert.That(CustomMarkingRules.Toggle(worn, c, 2), Is.True);
        Assert.That(worn, Is.EqualTo(new[] { b, c }));
    }

    [Test]
    public void ApplySavedTest()
    {
        var a = new CustomMarkingEntry(1, "A", HashA, CustomMarkingPlacement.Skin);
        var redrawn = a with { Hash = HashB };
        var moved = a with { Placement = CustomMarkingPlacement.Hair };
        var worn = new List<CustomMarking>();

        // A new entry goes on while there is room, once.
        Assert.That(CustomMarkingRules.ApplySaved(worn, null, a, 2), Is.True);
        Assert.That(CustomMarkingRules.ApplySaved(worn, null, a, 2), Is.False);
        Assert.That(worn, Is.EqualTo(new[] { new CustomMarking(HashA, CustomMarkingPlacement.Skin) }));

        var other = new CustomMarking(new string('c', CustomMarkingRules.HashLength), CustomMarkingPlacement.Front);
        worn.Add(other);
        Assert.That(CustomMarkingRules.ApplySaved(worn, null, redrawn, 2), Is.False, "no room");

        // A worn entry that changes is swapped where it sits.
        Assert.That(CustomMarkingRules.ApplySaved(worn, a, redrawn, 2), Is.True);
        Assert.That(worn, Is.EqualTo(new[] { new CustomMarking(HashB, CustomMarkingPlacement.Skin), other }));
        Assert.That(CustomMarkingRules.ApplySaved(worn, redrawn, redrawn with { Name = "Renamed" }, 2), Is.False, "a rename changes nothing worn");
        Assert.That(CustomMarkingRules.ApplySaved(worn, redrawn, redrawn with { Placement = CustomMarkingPlacement.Hair }, 2), Is.True);
        Assert.That(worn[0], Is.EqualTo(new CustomMarking(HashB, CustomMarkingPlacement.Hair)));

        // An entry that isn't worn stays off.
        Assert.That(CustomMarkingRules.ApplySaved(worn, a, moved, 2), Is.False);
        Assert.That(worn, Has.Count.EqualTo(2));

        // Changing an entry into one already worn leaves a single copy.
        var wornEntry = new CustomMarkingEntry(3, "B", HashB, CustomMarkingPlacement.Hair);
        var twin = wornEntry with { Hash = other.Hash, Placement = other.Placement };
        Assert.That(CustomMarkingRules.ApplySaved(worn, wornEntry, twin, 2), Is.True);
        Assert.That(worn, Is.EqualTo(new[] { other }));
    }

    /// <summary>Frame times as they are stored beside art: two bytes a frame, and none for a still marking.</summary>
    [Test]
    public void FrameTimesTest()
    {
        Assert.That(CustomMarkingRules.PackFrameTimes(new[] { 200 }), Is.Null, "a still marking has no times worth keeping");

        var packed = CustomMarkingRules.PackFrameTimes(new[] { 100, 300, 10000 });
        Assert.That(packed, Is.EqualTo(new byte[] { 100, 0, 44, 1, 16, 39 }));
        Assert.That(CustomMarkingRules.UnpackFrameTimes(packed), Is.EqualTo(new[] { 100, 300, 10000 }));

        Assert.Multiple(() =>
        {
            Assert.That(CustomMarkingRules.UnpackFrameTimes(null), Is.Null);
            Assert.That(CustomMarkingRules.UnpackFrameTimes(new byte[] { 1, 2 }), Is.Null, "one frame is no animation");
            Assert.That(CustomMarkingRules.UnpackFrameTimes(new byte[] { 1, 2, 3, 4, 5 }), Is.Null, "half a time");
            Assert.That(CustomMarkingRules.MaxFrameTime, Is.LessThanOrEqualTo(ushort.MaxValue), "a time fits its two bytes");
        });
    }
}
