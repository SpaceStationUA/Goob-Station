using Content.Shared._DV.CCVars;
using Content.Shared._Pirate.Silicons.IPC;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
namespace Content.Client._Pirate.Silicons.IPC;

/// <summary>
/// Manages the screen vision overlays for players with
/// <see cref="ScreenVisionComponent"/>: the constant CRT filter, plus an
/// optional glitch effect whose intensity ramps up as the owner loses health
/// (crit/dead glitch hard, heavy damage glitches softly).
/// Everything respects the "Disable vision filters" accessibility option
/// (<see cref="DCCVars.NoVisionFilters"/>).
/// </summary>
public sealed partial class ScreenVisionSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ISharedPlayerManager _playerMan = default!;

    private ScreenVisionOverlay _overlay = default!;
    private IPCHealthGlitchOverlay _glitchOverlay = default!;

    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ScreenVisionComponent, MapInitEvent>(OnScreenVisionMapInit);
        SubscribeLocalEvent<ScreenVisionComponent, ComponentShutdown>(OnScreenVisionShutdown);
        SubscribeLocalEvent<ScreenVisionComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<ScreenVisionComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);

        // NOTE(Pirate: port): do not rely on DamageChangedEvent/MobStateChangedEvent here -
        // they are mostly processed server-side and their component-state changes
        // are not guaranteed to re-raise them on the client, so the glitch would
        // never update. We poll the local player's health in Update() instead.

        Subs.CVar(_cfg, DCCVars.NoVisionFilters, OnNoVisionFiltersChanged);

        _overlay = new();
        _glitchOverlay = new();
        _enabled = !_cfg.GetCVar(DCCVars.NoVisionFilters);
        UpdateOverlays();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        UpdateOverlays(false);
    }

    private void OnScreenVisionMapInit(Entity<ScreenVisionComponent> entity, ref MapInitEvent args)
    {
        if (entity.Comp.HealthGlitch)
            UpdateGlitchStrength(entity);
    }

    private const float GlitchUpdateInterval = 0.25f;
    private float _glitchUpdateAccumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _glitchUpdateAccumulator += frameTime;
        if (_glitchUpdateAccumulator < GlitchUpdateInterval)
            return;

        _glitchUpdateAccumulator = 0f;

        if (_playerMan.LocalEntity is not { Valid: true } player)
            return;

        if (!EntityManager.TryGetComponent<ScreenVisionComponent>(player, out var comp))
            return;

        UpdateGlitchStrength((player, comp));
    }

    private void OnScreenVisionShutdown(Entity<ScreenVisionComponent> entity, ref ComponentShutdown args)
    {
        if (entity.Owner != _playerMan.LocalEntity)
            return;

        UpdateOverlays(false);
    }

    private void OnPlayerAttached(Entity<ScreenVisionComponent> entity, ref LocalPlayerAttachedEvent args)
    {
        UpdateOverlays();
        UpdateGlitchStrength(entity);
    }

    private void OnPlayerDetached(Entity<ScreenVisionComponent> entity, ref LocalPlayerDetachedEvent args)
    {
        UpdateOverlays(false);
    }

    private void OnNoVisionFiltersChanged(bool enabled)
    {
        _enabled = !enabled;
        UpdateOverlays();
    }

    private void UpdateOverlays(bool add = true)
    {
        var want = add && _enabled;

        if (want)
        {
            _overlayMan.AddOverlay(_overlay);
            _overlayMan.AddOverlay(_glitchOverlay);
        }
        else
        {
            _overlayMan.RemoveOverlay(_overlay);
            _overlayMan.RemoveOverlay(_glitchOverlay);
            _glitchOverlay.SetStrength(0f);
        }

        if (_playerMan.LocalEntity is { Valid: true } player
            && EntityManager.TryGetComponent<ScreenVisionComponent>(player, out var comp))
        {
            UpdateGlitchStrength((player, comp));
        }
    }

    /// <summary>
    /// Computes and applies the glitch intensity for the local player's health:
    /// dead > critical > scaled down to 0 with damage. No damage = no glitch.
    /// </summary>
    private void UpdateGlitchStrength(Entity<ScreenVisionComponent> entity)
    {
        var strength = 0f;

        if (_enabled && entity.Comp.HealthGlitch)
        {
            if (EntityManager.TryGetComponent<MobStateComponent>(entity, out var state))
            {
                switch (state.CurrentState)
                {
                    case MobState.Dead:
                        strength = 1f;
                        break;
                    case MobState.Critical:
                        strength = 0.7f;
                        break;
                    case MobState.Alive:
                        if (EntityManager.TryGetComponent<DamageableComponent>(entity, out var damageable))
                            strength = Math.Clamp((damageable.TotalDamage.Float() - 20f) / 40f, 0f, 0.6f);
                        break;
                }
            }
        }

        _glitchOverlay.SetStrength(strength);
    }
}
