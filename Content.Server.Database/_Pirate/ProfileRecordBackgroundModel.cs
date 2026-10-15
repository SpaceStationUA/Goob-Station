// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Content.Server.Database;
public sealed class ProfileRecordBackground
{
    public int Book { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;
}

public partial class Profile
{
    public List<ProfileRecordBackground> RecordBackgrounds { get; set; } = new();
}

internal static class ProfileRecordBackgroundModel
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Profile>()
            .Property(profile => profile.RecordBackgrounds)
            .HasConversion(
                value => JsonSerializer.Serialize(value, (JsonSerializerOptions?) null),
                value => Deserialize(value))
            .Metadata.SetValueComparer(new ValueComparer<List<ProfileRecordBackground>>(
                (left, right) => Serialize(left) == Serialize(right),
                value => Serialize(value).GetHashCode(),
                value => Deserialize(Serialize(value))));
    }

    private static string Serialize(List<ProfileRecordBackground>? value)
    {
        return JsonSerializer.Serialize(value ?? new List<ProfileRecordBackground>(), (JsonSerializerOptions?) null);
    }

    private static List<ProfileRecordBackground> Deserialize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? new List<ProfileRecordBackground>()
            : JsonSerializer.Deserialize<List<ProfileRecordBackground>>(value, (JsonSerializerOptions?) null)
              ?? new List<ProfileRecordBackground>();
    }
}
