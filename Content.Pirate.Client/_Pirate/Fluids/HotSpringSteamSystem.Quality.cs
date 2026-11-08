using Content.Goobstation.Common.CCVar;
using Content.Pirate.Shared.Fluids;
using Robust.Shared.Configuration;

namespace Content.Pirate.Client.Fluids;

public sealed partial class HotSpringSteamSystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private bool _refreshEmitters;

    private void InitializeParticleQuality()
    {
        _cfg.OnValueChanged(GoobCVars.ParticleQuality, OnParticleQualityChanged);
    }

    private void ShutdownParticleQuality()
    {
        _cfg.UnsubValueChanged(GoobCVars.ParticleQuality, OnParticleQualityChanged);
    }

    private void OnParticleQualityChanged(int quality)
    {
        foreach (var emitter in _emitters.Values)
            _particles.RemoveParticle(emitter);
        _emitters.Clear();
        _refreshEmitters = quality != 0;
    }

    // ParticleSystem must receive its quality callback before emitters are created.
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (!_refreshEmitters)
            return;

        _refreshEmitters = false;
        var query = AllEntityQuery<HotSpringSteamComponent>();
        while (query.MoveNext(out var uid, out var steam))
        {
            if (steam.Running && !TerminatingOrDeleted(uid))
                CreateEmitter((uid, steam));
        }
    }
}
