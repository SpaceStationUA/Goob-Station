// SPDX-License-Identifier: AGPL-3.0-or-later
// Pirate - ported from Trauma Station

using Content.Pirate.Common.Input;
using Content.Shared.CombatMode;
using Robust.Shared.Input.Binding;
using Robust.Shared.Player;

namespace Content.Pirate.Shared.Movement;

/// <summary>
/// Faces the controlled entity toward the cursor while the strafe key is held.
/// </summary>
public sealed partial class StrafingSystem : EntitySystem
{
    [Dependency] private readonly SharedCombatModeSystem _combat = default!;

    private EntityQuery<CombatModeComponent> _combatQuery;

    public override void Initialize()
    {
        base.Initialize();

        _combatQuery = GetEntityQuery<CombatModeComponent>();

        CommandBinds.Builder
            .Bind(PirateKeyFunctions.Strafe,
                InputCmdHandler.FromDelegate(
                    session => ToggleRotator(session, true),
                    session => ToggleRotator(session, false),
                    handle: false,
                    outsidePrediction: false))
            .Register<StrafingSystem>();
    }

    public override void Shutdown()
    {
        CommandBinds.Unregister<StrafingSystem>();
        base.Shutdown();
    }

    private void ToggleRotator(ICommonSession? session, bool enabled)
    {
        if (session?.AttachedEntity is not { } entity)
            return;

        // Combat mode owns the same components and must keep them after Shift is released.
        if (_combatQuery.CompOrNull(entity) is { ToggleMouseRotator: true, IsInCombatMode: true })
            return;

        _combat.SetMouseRotatorComponents(entity, enabled);
    }
}
