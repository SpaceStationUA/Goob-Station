// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.EntityEffects;
using Robust.Shared.GameStates;
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
/// While the effect lasts the subject gets a green tint and glow — a visible power-up
/// tell so other players can tell they are untouchable. The server subclass handles the
/// glow light, the client subclass applies the sprite tint locally.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public abstract partial class SharedTemporaryGodmodeSystem : EntityEffectSystem<MetaDataComponent, TemporaryGodmode>
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
        // The component carries the visual tell, so it lives for the whole window and
        // every removal path (timer, deletion) undoes its parts via remove events.
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
/// Tracks a temporary godmode application so its removal timer can be refreshed or
/// cancelled, and so the green power-up tell can be undone afterwards.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class TemporaryGodmodeComponent : Component
{
    /// <summary>Color of the tint and glow applied while invulnerable.</summary>
    [DataField]
    public Color TintColor = Color.FromHex("#73FF73");

    [DataField]
    public float LightRadius = 1.5f;

    [DataField]
    public float LightEnergy = 1.5f;

    /// <summary>Whether the entity already had a point light before the glow was added.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public bool HadPointLight;

    /// <summary>Sprite color before the invincibility tint was applied.</summary>
    [ViewVariables(VVAccess.ReadOnly)]
    public Color OldColor = Color.White;

    [ViewVariables]
    public CancellationTokenSource? CancelToken;
}
