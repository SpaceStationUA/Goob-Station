// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared.Arcade;

/// <summary>
///     A game shipped with the Pirate WebArcade. The cabinet loads
///     <see cref="Path"/> (a res:// page) as an interactive or mirrored
///     browser window. Defined in Prototypes/_Pirate/Arcade/games.yml so
///     adding a game is a YAML edit, not a code change.
/// </summary>
[Prototype("arcadeGame")]
public sealed partial class PirateArcadeGamePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Human-readable name shown on the cabinet's picker.</summary>
    [DataField(required: true)]
    public string Label { get; private set; } = default!;

    /// <summary>Page to load, relative to the content root (no res://
    /// prefix; the window adds it). E.g.
    /// "_Pirate/WebUI/Arcade/Foo/index.html".</summary>
    [DataField(required: true)]
    public string Path { get; private set; } = default!;

    /// <summary>Load this on a fresh cabinet. Exactly one game should set
    /// it; otherwise the first declared game is used.</summary>
    [DataField]
    public bool Default { get; private set; }
}

/// <summary>
///     Convenience access to the arcade-game prototypes. Both halves use
///     this so the client's picker and the server's "is this a real game?"
///     check agree. Order is the prototype declaration order.
/// </summary>
public static class PirateArcadeGames
{
    private static IPrototypeManager? _prototypes;

    /// <summary>Bind the prototype manager once (called from both halves'
    /// systems on initialize).</summary>
    public static void Initialize(IPrototypeManager prototypes) => _prototypes = prototypes;

    /// <summary>All shipped games, in YAML order.</summary>
    public static IEnumerable<PirateArcadeGamePrototype> All
        => _prototypes != null
            ? _prototypes.EnumeratePrototypes<PirateArcadeGamePrototype>()
            : System.Array.Empty<PirateArcadeGamePrototype>();

    public static bool Exists(string id)
    {
        if (_prototypes == null || string.IsNullOrEmpty(id))
            return false;
        return _prototypes.HasIndex<PirateArcadeGamePrototype>(id);
    }

    public static PirateArcadeGamePrototype? Get(string id)
    {
        if (_prototypes == null || string.IsNullOrEmpty(id))
            return null;
        return _prototypes.TryIndex<PirateArcadeGamePrototype>(id, out var proto) ? proto : null;
    }

    /// <summary>The game a fresh cabinet loads: the one flagged Default, or
    /// the first declared if none is.</summary>
    public static PirateArcadeGamePrototype? DefaultGame()
    {
        PirateArcadeGamePrototype? first = null;
        foreach (var game in All)
        {
            first ??= game;
            if (game.Default)
                return game;
        }
        return first;
    }
}
