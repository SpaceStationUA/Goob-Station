// SPDX-FileCopyrightText: 2024 creku <czevanoi@gmail.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Text.RegularExpressions;
using Content.Pirate.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Pirate.Server.Speech.Systems;

public sealed class MonotonousSystem : EntitySystem
{
    private static readonly Regex RegexAnyPunctuationNotPeriod = new(@"[!?]+");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MonotonousComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<MonotonousComponent> entity, ref AccentGetEvent args)
    {
        args.Message = RegexAnyPunctuationNotPeriod.Replace(args.Message, ".");

        if (!char.IsLetterOrDigit(args.Message[^1]) && !char.IsPunctuation(args.Message[^1]))
            args.Message += ".";
    }
}
