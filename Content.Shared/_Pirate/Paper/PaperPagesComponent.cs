// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-only

using System;
using System.Collections.Generic;
using System.Text;
using Content.Shared.Paper;
using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.Paper;

/// <summary>
/// Pirate: persistent diary pages - splits a paper-like item into leaves that can be
/// flipped like a book, added and torn out one by one.
/// The leaves are persisted inside <see cref="PaperComponent.Content"/>, so every existing
/// persistence/fax/print flow keeps working unchanged (a saved diary is just one flat string).
/// Server-side only: the client only ever sees the leaf currently on screen, delivered
/// through the paper's bound UI state.
/// </summary>
[RegisterComponent]
public sealed partial class PaperPagesComponent : Component
{
    /// <summary>
    /// The leaves of the book. Never kept empty.
    /// </summary>
    [DataField]
    public List<PaperPage> Pages = new() { new() };

    /// <summary>
    /// The leaf everyone is currently looking at. Like <see cref="PaperComponent.Mode"/>
    /// this is entity-wide rather than per viewer, so it is never networked.
    /// </summary>
    [DataField]
    public int CurrentPage;
}

/// <summary>
/// A single leaf: the text written on it and everyone who signed it.
/// </summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class PaperPage
{
    [DataField]
    public string Content = string.Empty;

    /// <summary>
    /// Signatures of this leaf only. Deliberately kept out of <see cref="PaperComponent.StampedBy"/>:
    /// a stamp there locks the paper forever, which would make a diary unwritable
    /// (see <see cref="NoStampingComponent"/>).
    /// </summary>
    [DataField]
    public List<StampDisplayInfo> Signatures = new();
}

/// <summary>
/// Serializes leaves into the flat string stored in <see cref="PaperComponent.Content"/>.
/// Only control characters are used as separators, so they can never show up in typed text.
/// </summary>
public static class PaperPageFormat
{
    /// <summary>Form feed; separates one leaf from the next.</summary>
    public const char LeafSeparator = '\f';

    /// <summary>Group separator; separates leaf text from its signatures.</summary>
    public const char SignaturesMarker = '\u001D';

    /// <summary>Record separator; separates individual signatures of one leaf.</summary>
    public const char SignatureSeparator = '\u001E';

    // Stamp frame: \u0017#RRGGBBAA\u0017name.
    public const char StampMarker = '\u0017';

    /// <summary>Ink used for leaf signatures, matching the pen signature stamp.</summary>
    public static readonly Color SignatureColor = Color.DarkSlateGray;

    // Pen signatures always use SignatureColor.
    public static bool IsStamp(StampDisplayInfo info) => info.StampedColor != SignatureColor;

    public static string Encode(IReadOnlyList<PaperPage> pages)
    {
        var builder = new StringBuilder();

        for (var i = 0; i < pages.Count; i++)
        {
            if (i > 0)
                builder.Append(LeafSeparator);

            var page = pages[i];
            builder.Append(page.Content);

            if (page.Signatures.Count == 0)
                continue;

            builder.Append(SignaturesMarker);
            for (var j = 0; j < page.Signatures.Count; j++)
            {
                if (j > 0)
                    builder.Append(SignatureSeparator);

                var signature = page.Signatures[j];
                if (IsStamp(signature))
                    builder.Append(StampMarker).Append(signature.StampedColor.ToHex()).Append(StampMarker);

                builder.Append(signature.StampedName);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Strips every control character this format (or the hidden cover meta block) uses as
    /// a separator from player-written text. None of them can be typed legitimately, and a
    /// forged one would otherwise be decoded into a real leaf break or signature.
    /// </summary>
    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            // Reserve document separators, legacy cover delimiters (U+001C/U+001F), and the stamp marker.
            if (c == LeafSeparator || c is '\u001C' or '\u001D' or '\u001E' or '\u001F' or StampMarker)
                continue;

            builder.Append(c);
        }

        return builder.ToString();
    }

    // Legacy cover metadata would otherwise appear as text on the first page.
    public static string StripLegacyCoverMeta(string content)
    {
        if (content.Length < 2 || content[0] != '\u001C')
            return content;

        var end = content.IndexOf('\u001C', 1);
        return end < 0 ? content : content[(end + 1)..];
    }

    // Stamps must not persist between rounds; pen signatures may persist.
    public static string StripStamps(string content)
    {
        var pages = Decode(content);
        foreach (var page in pages)
        {
            page.Signatures.RemoveAll(IsStamp);
        }

        return Encode(pages);
    }

    /// <summary>
    /// Turns a document back into leaves. Documents saved before paging existed simply
    /// decode into a single leaf holding their whole text.
    /// </summary>
    public static List<PaperPage> Decode(string content)
    {
        var pages = new List<PaperPage>();

        foreach (var leaf in content.Split(LeafSeparator))
        {
            var page = new PaperPage();
            var marker = leaf.IndexOf(SignaturesMarker);

            page.Content = marker < 0 ? leaf : leaf[..marker];

            if (marker >= 0)
            {
                foreach (var entry in leaf[(marker + 1)..].Split(SignatureSeparator))
                {
                    var name = entry;
                    var color = SignatureColor;

                    // Malformed stamp frames fall back to pen signatures.
                    if (name.Length > 0 && name[0] == StampMarker)
                    {
                        var end = name.IndexOf(StampMarker, 1);
                        if (end > 0)
                        {
                            if (Color.TryFromHex(name.AsSpan(1, end - 1), out var ink))
                                color = ink;
                            name = name[(end + 1)..];
                        }
                    }

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    page.Signatures.Add(new StampDisplayInfo
                    {
                        StampedName = name,
                        StampedColor = color,
                    });
                }
            }

            pages.Add(page);
        }

        if (pages.Count == 0)
            pages.Add(new PaperPage());

        return pages;
    }
}
