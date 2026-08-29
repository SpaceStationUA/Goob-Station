// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Content.Server.Database;

public partial class Profile
{
    public Dictionary<string, int> KnowledgeMastery { get; set; } = new();
}

internal static class KnowledgeProfileModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>()
            .Property(profile => profile.KnowledgeMastery)
            .HasConversion(
                value => JsonSerializer.Serialize(value),
                value => string.IsNullOrWhiteSpace(value)
                    ? new Dictionary<string, int>()
                    : JsonSerializer.Deserialize<Dictionary<string, int>>(value) ?? new Dictionary<string, int>())
            .Metadata.SetValueComparer(new ValueComparer<Dictionary<string, int>>(
                (left, right) => DictionariesEqual(left, right),
                value => StructuralHash(value),
                value => new Dictionary<string, int>(value)));
    }

    private static bool DictionariesEqual(Dictionary<string, int>? left, Dictionary<string, int>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) || value != other)
                return false;
        }

        return true;
    }

    private static int StructuralHash(Dictionary<string, int> value)
    {
        var hash = new HashCode();
        foreach (var pair in value.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            hash.Add(pair.Key, StringComparer.Ordinal);
            hash.Add(pair.Value);
        }
        return hash.ToHashCode();
    }
}
