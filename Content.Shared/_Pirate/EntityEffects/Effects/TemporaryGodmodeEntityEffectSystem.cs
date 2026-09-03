// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
namespace Content.Shared._Pirate.EntityEffects.Effects;

/// <summary>
/// Grants temporary invulnerability through the godmode system, then removes it.
/// Used by event items such as the invisible chocolate bar. Re-doses refresh the timer
/// instead of stacking, and the invulnerability is never left stuck: the removal timer
/// always runs even if the reagent stops metabolizing.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class TemporaryGodmodeEntityEffectSystem : EntityEffectSystem<MetaDataComponent, TemporaryGodmode>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedGodmodeSystem _godmode = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<TemporaryGodmode> args)
    {
        // Godmode is not networked; metabolism runs on the server anyway.
        if (_net.IsClient)
            return;

        var uid = entity.Owner;

        // Re-doses refresh the timer instead of extending godmode indefinitely.
        var temp = EntityManager.EnsureComponent<TemporaryGodmodeComponent>(uid);
        temp.CancelToken?.Cancel();
        temp.CancelToken = new CancellationTokenSource();
        var token = temp.CancelToken.Token;

        _godmode.EnableGodmode(uid);

        Timer.Spawn(TimeSpan.FromSeconds(args.Effect.Seconds), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            _godmode.DisableGodmode(uid);
            EntityManager.RemoveComponentDeferred<TemporaryGodmodeComponent>(uid);
        }, token);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class TemporaryGodmode : EntityEffectBase<TemporaryGodmode>
{
    /// <summary>
    /// How long invulnerability lasts after the last dose, in seconds.
    /// </summary>
    [DataField]
    public float Seconds = 15f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-temporary-godmode", ("seconds", (int) Seconds));
}

/// <summary>
/// Tracks a temporary godmode application so its removal timer can be refreshed or cancelled.
/// </summary>
[RegisterComponent]
public sealed partial class TemporaryGodmodeComponent : Component
{
    [ViewVariables]
    public CancellationTokenSource? CancelToken;
}
