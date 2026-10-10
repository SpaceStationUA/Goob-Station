// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading;
using Content.Shared.EntityEffects;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Shared._Pirate.EntityEffects.Effects;

/// <summary>
/// Makes the subject invisible through the stealth system for a while, then undoes it.
/// Used by event items such as the ghost chocolate bar. Re-doses refresh the timer
/// instead of stacking. Attacking or taking damage reveals the subject again, since
/// StealthComponent does that on its own.
/// </summary>
/// <inheritdoc cref="EntityEffectSystem{T,TEffect}"/>
public sealed partial class TemporaryInvisibilityEntityEffectSystem : EntityEffectSystem<MetaDataComponent, TemporaryInvisibility>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;

    protected override void Effect(Entity<MetaDataComponent> entity, ref EntityEffectEvent<TemporaryInvisibility> args)
    {
        // The stealth state syncs on its own; metabolism runs on the server anyway.
        if (_net.IsClient)
            return;

        var uid = entity.Owner;

        var hadEffect = TryComp<TemporaryInvisibilityComponent>(uid, out var temp);
        temp ??= AddComp<TemporaryInvisibilityComponent>(uid);

        if (!hadEffect)
        {
            // Save the stealth state the subject had before the effect so it can be
            // restored on shutdown. On re-doses the original state is kept.
            var hadStealth = TryComp<StealthComponent>(uid, out var stealth);
            stealth ??= EnsureComp<StealthComponent>(uid);

            temp.HadStealth = hadStealth;
            temp.WasEnabled = stealth.Enabled;
            temp.OldVisibility = _stealth.GetVisibility(uid, stealth);
            temp.ThermalsImmune = stealth.ThermalsImmune;

            _stealth.SetEnabled(uid, true, stealth);
            _stealth.SetVisibility(uid, args.Effect.Visibility, stealth);
        }

        // Re-doses refresh the timer instead of extending invisibility indefinitely.
        temp.CancelToken?.Cancel();
        temp.CancelToken = new CancellationTokenSource();
        var token = temp.CancelToken.Token;

        Timer.Spawn(TimeSpan.FromSeconds(args.Effect.Seconds), () =>
        {
            if (TerminatingOrDeleted(uid))
                return;

            EntityManager.RemoveComponentDeferred<TemporaryInvisibilityComponent>(uid);
        }, token);
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TemporaryInvisibilityComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnShutdown(Entity<TemporaryInvisibilityComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (!TryComp<StealthComponent>(ent, out var stealth))
            return;

        _stealth.SetVisibility(ent, ent.Comp.OldVisibility, stealth);
        _stealth.SetEnabled(ent, ent.Comp.WasEnabled, stealth);
        _stealth.SetThermalsImmune(ent, ent.Comp.ThermalsImmune, stealth);

        // If the effect added the stealth itself, leave it behind but disabled.
        if (!ent.Comp.HadStealth)
            _stealth.SetEnabled(ent, false, stealth);
    }
}

/// <inheritdoc cref="EntityEffect"/>
public sealed partial class TemporaryInvisibility : EntityEffectBase<TemporaryInvisibility>
{
    /// <summary>
    /// How long invisibility lasts after the last dose, in seconds.
    /// </summary>
    [DataField]
    public float Seconds = 15f;

    /// <summary>
    /// Target visibility while the effect lasts. -1.5 is fully hidden, values towards 1 fade back in.
    /// </summary>
    [DataField]
    public float Visibility = -1.5f;

    public override string? EntityEffectGuidebookText(IPrototypeManager prototype, IEntitySystemManager entSys)
        => Loc.GetString("reagent-effect-guidebook-temporary-invisibility", ("seconds", (int) Seconds));
}

/// <summary>
/// Marks a temporary invisibility application so its removal timer can be refreshed or
/// cancelled, and keeps the stealth state to restore when the effect ends.
/// </summary>
[RegisterComponent]
public sealed partial class TemporaryInvisibilityComponent : Component
{
    [ViewVariables]
    public CancellationTokenSource? CancelToken;

    [ViewVariables]
    public bool HadStealth;

    [ViewVariables]
    public bool WasEnabled;

    [ViewVariables]
    public float OldVisibility;

    [ViewVariables]
    public bool ThermalsImmune;
}
