// SPDX-FileCopyrightText: 2025 pocl.v <24708225+Pinkbat5@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Pirate.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Pirate.Server.Speech.Systems;

public sealed class ProperCapitalizationSystem : EntitySystem
{
    private static readonly Regex RegexPunctuationThenWord = new(@"([.!?])\s+([a-z])");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProperCapitalizationComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<ProperCapitalizationComponent> entity, ref AccentGetEvent args)
    {
        args.Message = RegexPunctuationThenWord.Replace(args.Message,
            match => match.Groups[1].Value + " " + match.Groups[2].Value.ToUpper());
    }
}
