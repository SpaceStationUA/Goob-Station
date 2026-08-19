using System.Diagnostics.CodeAnalysis;
using Content.Pirate.Shared.ModularSuit;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.ModularSuit;

public abstract partial class ModuleActionHandler : EntitySystem
{
    [Dependency] protected SharedAudioSystem Audio = default!;
    [Dependency] protected SharedContainerSystem Container = default!;
    [Dependency] protected ModularSuitSystem ModularSuit = default!;
    [Dependency] protected SharedPopupSystem Popup = default!;

    /// <param name="requireActive">
    /// When false, the modules can be reached even while the suit is not deployed.
    /// Only for modules that are purely mechanical and stay usable on an unsealed suit, like the holster.
    /// </param>
    public BaseContainer? GetModulesContainer(EntityUid suitUid, bool requireActive = true)
    {
        if (!TryComp<ModularSuitComponent>(suitUid, out var suit) || requireActive && !suit.Active)
            return null;

        return Container.GetContainer(suitUid, ModularSuitSystem.ModuleContainer);
    }

    /// <inheritdoc cref="GetModulesContainer"/>
    public bool TryFindModuleByAction(Entity<ModularSuitActionHolderComponent> suit, EntityUid actionUid, [NotNullWhen(true)] out EntityUid? moduleEnt, bool requireActive = true)
    {
        moduleEnt = null;

        EntProtoId? actionId = null;
        foreach (var kvp in suit.Comp.ModuleActions)
        {
            if (kvp.Value == actionUid)
            {
                actionId = kvp.Key;
                break;
            }
        }

        if (actionId == null)
            return false;

        var container = GetModulesContainer(suit, requireActive);
        if (container == null)
            return false;

        foreach (var module in container.ContainedEntities)
        {
            if (!TryComp<ModularSuitActionModuleComponent>(module, out var moduleAction))
                continue;

            if (moduleAction.Action == actionId)
            {
                moduleEnt = module;
                return true;
            }
        }

        return false;
    }
}
