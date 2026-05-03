using Content.Server.Abilities.Psionics;
using Content.Server.GameTicking.Rules;
using Content.Shared.Abilities.Psionics;
using Content.Shared.Humanoid;
using Content.Shared.Psionics;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.MakeAPsionic;

public sealed class MakeAPsionicSystem : EntitySystem
{
    [Dependency] private readonly PsionicAbilitiesSystem _psionicAbilitiesSystem = default!;

    public void MakePsionic(PsionicPowerPrototype psionicPower, EntityUid entity)
    {
        EnsureComp<PsionicComponent>(entity);
        _psionicAbilitiesSystem.InitializePsionicPower(entity, psionicPower);
    }
}
