// Pirate: preserve Starlight's alternate unpacking verb with native item spawning.
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Verbs;

namespace Content.Server._Pirate.StarlightSpecies;

[RegisterComponent]
public sealed partial class StarlightWrapperComponent : Component;

public sealed class StarlightWrapperSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<StarlightWrapperComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<StarlightWrapperComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || _hands.IsHolding(args.User, ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("delivery-open-verb"),
            Act = () => RaiseLocalEvent(ent.Owner, new UseInHandEvent(user)),
        });
    }
}
