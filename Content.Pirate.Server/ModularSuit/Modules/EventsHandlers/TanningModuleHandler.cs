using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Humanoid;

namespace Content.Pirate.Server.ModularSuit;

public sealed partial class TanningModuleHandler : ModuleActionHandler
{
    [Dependency] private SharedHumanoidAppearanceSystem _humanoid = default!;

    private const float MinColorValue = 0.3f;
    private const float DarkenFactor = 0.85f;

    public override void Initialize()
    {
        SubscribeLocalEvent<ModularSuitActionHolderComponent, ActivateTanningModuleEvent>(OnTan);
    }

    private void OnTan(Entity<ModularSuitActionHolderComponent> ent, ref ActivateTanningModuleEvent args)
    {
        if (args.Handled)
            return;

        if (!TryFindModuleByAction(ent, args.Action, out var moduleEnt))
            return;

        if (!TryComp<ModularSuitModuleComponent>(moduleEnt, out var moduleComp) || !moduleComp.IsActive)
            return;

        var user = args.Performer;
        if (!TryComp<HumanoidAppearanceComponent>(user, out var humanoid))
            return;

        var currentColor = humanoid.SkinColor;

        if (currentColor.R <= MinColorValue
            && currentColor.G <= MinColorValue
            && currentColor.B <= MinColorValue)
        {
            Popup.PopupEntity(Loc.GetString("modsuit-tanning-max"), user, user);
            return;
        }

        if (!ModularSuit.TryUseCoreCharge(ent.Owner, moduleComp.PowerInstanceUsage))
            return;

        var newColor = new Color(
            Math.Max(MinColorValue, currentColor.R * DarkenFactor),
            Math.Max(MinColorValue, currentColor.G * DarkenFactor),
            Math.Max(MinColorValue, currentColor.B * DarkenFactor)
        );

        // Pirate: SharedVisualBodySystem was replaced upstream by humanoid appearance APIs.
        _humanoid.SetSkinColor(user, newColor, verify: false, humanoid: humanoid);

        Popup.PopupEntity(Loc.GetString("modsuit-tanning-used"), user, user);

        args.Handled = true;
    }
}
