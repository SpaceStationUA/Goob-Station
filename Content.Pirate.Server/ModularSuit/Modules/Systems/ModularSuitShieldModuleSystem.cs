// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.ModularSuit;
using Content.Shared._Mono.PersonalShield;
using Robust.Shared.Audio.Systems;

namespace Content.Pirate.Server.ModularSuit;

/// <summary>
/// Pirate: ERT modsuits - drives <see cref="ModularSuitShieldModuleComponent"/>.
/// </summary>
public sealed partial class ModularSuitShieldModuleSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModularSuitShieldModuleComponent, ModularSuitModuleToggledEvent>(OnToggled);
        SubscribeLocalEvent<ModularSuitShieldModuleComponent, ModularSuitRemovedEvent>(OnRemoved);
    }

    private void OnToggled(Entity<ModularSuitShieldModuleComponent> module, ref ModularSuitModuleToggledEvent args)
    {
        if (args.Activated)
            Raise(module, args.Wearer);
        else
            Lower(module);
    }

    private void OnRemoved(Entity<ModularSuitShieldModuleComponent> module, ref ModularSuitRemovedEvent args)
    {
        Lower(module);
    }

    private void Raise(Entity<ModularSuitShieldModuleComponent> module, EntityUid? wearer)
    {
        if (wearer is not { } user || TerminatingOrDeleted(user))
            return;

        // ComponentStartup runs before SelfDriven is set, so initialize new shields here.
        var existed = EnsureComp<PersonalShieldComponent>(user, out var shield);
        shield.SelfDriven = true;
        shield.Enabled = true;
        shield.Shield = module.Comp.Shield;
        if (!existed)
            shield.Runtime.Charge = shield.Shield.MaxCharge;
        shield.Color = module.Comp.ShieldColor;
        Dirty(user, shield);

        module.Comp.Wearer = user;
        _audio.PlayPvs(module.Comp.ActivateSound, user);
    }

    private void Lower(Entity<ModularSuitShieldModuleComponent> module)
    {
        if (module.Comp.Wearer is not { } user)
            return;

        module.Comp.Wearer = null;

        if (TerminatingOrDeleted(user) || !TryComp<PersonalShieldComponent>(user, out var shield))
            return;

        shield.Enabled = false;
        Dirty(user, shield);

        _audio.PlayPvs(module.Comp.DeactivateSound, user);
    }
}
