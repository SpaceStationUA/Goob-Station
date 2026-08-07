// SPDX-FileCopyrightText: 2025 pocl.v <24708225+Pinkbat5@users.noreply.github.com>

// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Server.Speech.Components;
using Content.Shared.Speech;

namespace Content.Pirate.Server.Speech.Systems;

public sealed class ProperPunctuationSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ProperPunctuationComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(Entity<ProperPunctuationComponent> entity, ref AccentGetEvent args)
    {
        if (string.IsNullOrWhiteSpace(args.Message))
            return;

        if (!char.IsPunctuation(args.Message[^1]))
            args.Message += ".";
    }
}
