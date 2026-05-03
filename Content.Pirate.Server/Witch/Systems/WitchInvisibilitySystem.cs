using Content.Pirate.Shared.Witch.Components;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;

namespace Content.Pirate.Server.Witch.Systems;

public sealed class WitchInvisibilitySystem : EntitySystem
{
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    private readonly Dictionary<EntityUid, StealthState> _states = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WitchInvisibilityComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<WitchInvisibilityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnInit(Entity<WitchInvisibilityComponent> ent, ref ComponentInit args)
    {
        if (_states.ContainsKey(ent.Owner))
            return;

        var hadStealth = TryComp<StealthComponent>(ent.Owner, out var stealth);
        stealth ??= EnsureComp<StealthComponent>(ent.Owner);
        _states[ent.Owner] = new StealthState(hadStealth, stealth.Enabled, _stealth.GetVisibility(ent.Owner, stealth), stealth.ThermalsImmune);

        _stealth.SetEnabled(ent.Owner, true, stealth);
        _stealth.SetVisibility(ent.Owner, ent.Comp.Visibility, stealth);
        _stealth.SetThermalsImmune(ent.Owner, true, stealth);
    }

    private void OnShutdown(Entity<WitchInvisibilityComponent> ent, ref ComponentShutdown args)
    {
        if (!_states.Remove(ent.Owner, out var state) || !TryComp<StealthComponent>(ent.Owner, out var stealth))
            return;

        _stealth.SetVisibility(ent.Owner, state.Visibility, stealth);
        _stealth.SetEnabled(ent.Owner, state.Enabled, stealth);
        _stealth.SetThermalsImmune(ent.Owner, state.ThermalsImmune, stealth);

        if (!state.HadComponent)
            _stealth.SetEnabled(ent.Owner, false, stealth);
    }

    private readonly record struct StealthState(bool HadComponent, bool Enabled, float Visibility, bool ThermalsImmune);
}
