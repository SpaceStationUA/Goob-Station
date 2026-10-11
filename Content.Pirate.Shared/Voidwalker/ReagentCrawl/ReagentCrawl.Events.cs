// SPDX-FileCopyrightText: 2025 GoobBot <uristmchands@proton.me>
// SPDX-FileCopyrightText: 2025 Lumminal <81829924+Lumminal@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;

namespace Content.Pirate.Shared.Voidwalker.ReagentCrawl;

// Pirate: retain the generic crawl helper separately from upstream blood crawling.

public sealed partial class ReagentCrawlEvent : InstantActionEvent;

// Omu - Renamed to ReagentCrawl and genericized.

/// <summary>
/// Triggers once the entity activates the Reagent Crawl ability while not in Jaunt form.
/// </summary>
[ByRefEvent]
public record struct ReagentCrawlAttemptEvent(bool Cancelled = false);

/// <summary>
/// Triggers once the entity exits the Reagent Crawl ability.
/// </summary>
[ByRefEvent]
public record struct ReagentCrawlExitEvent(bool Cancelled = false);

/// <summary>
/// Triggers once the entity enters the Reagent Crawl ability in jaunt.
/// </summary>
[ByRefEvent]
public record struct ReagentCrawlEnterEvent(bool Cancelled = false);
