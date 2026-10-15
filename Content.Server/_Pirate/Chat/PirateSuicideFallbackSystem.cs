// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Chat;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared._White.Xenomorphs.Infection;
using Content.Shared.Chat;
using Content.Shared.Damage.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._Pirate.Chat;

/// <summary>
/// Applies default suicide damage after held items and nearby entities can handle the attempt.
/// </summary>
public sealed class PirateSuicideFallbackSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSuicideSystem _suicide = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, SuicideEvent>(OnSuicide,
            after: [typeof(SuicideSystem)]);
    }

    private void OnSuicide(Entity<DamageableComponent> victim, ref SuicideEvent args)
    {
        if (args.Handled || HasComp<XenomorphPreventSuicideComponent>(victim))
            return;

        var othersMessage = Loc.GetString("suicide-command-default-text-others", ("name", Identity.Entity(victim, EntityManager)));
        _popup.PopupEntity(othersMessage, victim, Filter.PvsExcept(victim), true);

        var selfMessage = Loc.GetString("suicide-command-default-text-self");
        _popup.PopupEntity(selfMessage, victim, victim);

        if (args.DamageSpecifier != null)
        {
            _suicide.ApplyLethalDamage(victim, args.DamageSpecifier);
            args.Handled = true;
            return;
        }

        if (HasComp<SiliconComponent>(victim))
            args.DamageType ??= "Shock";
        else
            args.DamageType ??= "Slash";

        _suicide.ApplyLethalDamage(victim, args.DamageType);
        args.Handled = true;
    }
}
