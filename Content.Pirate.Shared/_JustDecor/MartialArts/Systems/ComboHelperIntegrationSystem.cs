using Content.Pirate.Shared._JustDecor.MartialArts.Components;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Shared._JustDecor.MartialArts.Systems;

/// <summary>
/// Convenience helpers for other systems that want to add or reconfigure ComboHelper.
/// </summary>
public sealed class ComboHelperIntegrationSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ComboHelperSystem _comboSystem = default!;

    public bool TryAddComboHelper(EntityUid uid, string? helperPrototypeId = null, bool enabled = true)
    {
        if (helperPrototypeId != null && !_proto.HasIndex<CqcComboHelperPrototype>(helperPrototypeId))
        {
            Logger.WarningS("ComboHelper", $"Tried to add ComboHelper with non-existent prototype: {helperPrototypeId}");
            return false;
        }

        var helper = EnsureComp<ComboHelperComponent>(uid);

        if (helperPrototypeId != null)
            _comboSystem.SetHelperPrototype(uid, helperPrototypeId, helper);

        _comboSystem.SetEnabled(uid, enabled, helper);

        Logger.DebugS("ComboHelper",
            helperPrototypeId == null
                ? $"Added dynamic ComboHelper to {uid}"
                : $"Added ComboHelper to {uid} with prototype {helperPrototypeId}");
        return true;
    }

    public void RemoveComboHelper(EntityUid uid)
    {
        if (RemComp<ComboHelperComponent>(uid))
            Logger.DebugS("ComboHelper", $"Removed ComboHelper from {uid}");
    }

    public bool TryUpdateHelperPrototype(EntityUid uid, string newPrototypeId, ComboHelperComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
        {
            Logger.WarningS("ComboHelper", $"Tried to update helper prototype on {uid} without ComboHelperComponent");
            return false;
        }

        if (!_proto.HasIndex<CqcComboHelperPrototype>(newPrototypeId))
        {
            Logger.WarningS("ComboHelper", $"Tried to update to non-existent prototype: {newPrototypeId}");
            return false;
        }

        _comboSystem.SetHelperPrototype(uid, newPrototypeId, component);
        Logger.DebugS("ComboHelper", $"Updated helper prototype for {uid} to {newPrototypeId}");
        return true;
    }

    public bool HasActiveHelper(EntityUid uid, ComboHelperComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return false;

        return component.Enabled;
    }

    public string? GetHelperPrototypeId(EntityUid uid, ComboHelperComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return null;

        return component.Prototype?.Id;
    }
}
