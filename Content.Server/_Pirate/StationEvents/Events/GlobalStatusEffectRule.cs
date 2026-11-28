// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server._Pirate.StationEvents.Components;
using Content.Server.Chat.Systems;
using Content.Server.StationEvents.Events;
using Content.Shared.GameTicking.Components;
using Content.Shared.Humanoid;
using Content.Shared.Mind.Components;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Random;

namespace Content.Server._Pirate.StationEvents.Events;

/// <summary>
/// Applies a given status effect to every humanoid on the station for a random duration between max and min per-entity.
/// </summary>
public sealed class GlobalStatusEffectRule : StationEventSystem<GlobalStatusEffectRuleComponent>
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    protected override void Added(EntityUid uid, GlobalStatusEffectRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        if (!TryGetRandomStation(out _) || component.Announcement == null)
            return;

        _chat.DispatchGlobalAnnouncement(Loc.GetString(component.Announcement), colorOverride: Color.Gold);
    }

    protected override void Started(EntityUid uid, GlobalStatusEffectRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var query = EntityQueryEnumerator<MindContainerComponent, HumanoidAppearanceComponent>();
        while (query.MoveNext(out var ent, out var mindComp, out _))
        {
            if (!mindComp.HasMind)
                continue;

            TimeSpan? duration = null;
            if (component.MinEffectDuration is { } min && component.MaxEffectDuration is { } max)
                duration = TimeSpan.FromSeconds(_random.NextFloat(min, max));

            _statusEffect.TryUpdateStatusEffectDuration(ent, component.StatusEffect, duration);
        }
    }
}
