using Content.Goobstation.Client.Particles;
using Content.Pirate.Shared.Fluids;
using Robust.Shared.Random;

namespace Content.Pirate.Client.Fluids;

// Client entities received from the server do not raise MapInitEvent.
public sealed partial class HotSpringSteamSystem : EntitySystem
{
    [Dependency] private ParticleSystem _particles = default!;
    [Dependency] private IRobustRandom _random = default!;

    private readonly Dictionary<EntityUid, ActiveEmitter> _emitters = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HotSpringSteamComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HotSpringSteamComponent, ComponentShutdown>(OnShutdown);
        InitializeParticleQuality();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownParticleQuality();

        foreach (var emitter in _emitters.Values)
            _particles.RemoveParticle(emitter);
        _emitters.Clear();
    }

    private void OnStartup(Entity<HotSpringSteamComponent> ent, ref ComponentStartup args)
        => CreateEmitter(ent);

    private void CreateEmitter(Entity<HotSpringSteamComponent> ent)
    {
        var effects = ent.Comp.Effects;
        if (effects.Count == 0 || _emitters.ContainsKey(ent))
            return;

        // The same tile gets the same cloud on every client. Client-only entities have no net id.
        var netId = GetNetEntity(ent).Id;
        var seed = netId > 0 ? netId : ent.Owner.Id;
        var effect = effects[(int) ((uint) seed % (uint) effects.Count)];

        if (_particles.CreateParticle(effect, ent.Owner) is not { } emitter)
            return;

        // Randomize the start to prevent synchronized emission.
        emitter.EmitAccum = _random.NextFloat();
        _emitters[ent] = emitter;
    }

    private void OnShutdown(Entity<HotSpringSteamComponent> ent, ref ComponentShutdown args)
    {
        if (_emitters.Remove(ent, out var emitter))
            _particles.RemoveParticle(emitter);
    }
}
