using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Pirate.Client._Pirate.Audio;

/// <summary>
/// Keeps enough instances of the same sound available for station radio receivers on multiz maps.
/// </summary>
public sealed class PirateAudioConcurrentLimitSystem : EntitySystem
{
    private const int MinimumConcurrentSounds = 32;

    [Dependency] private readonly IConfigurationManager _configuration = default!;

    public override void Initialize()
    {
        base.Initialize();

        if (_configuration.GetCVar(CVars.AudioDefaultConcurrent) < MinimumConcurrentSounds)
            _configuration.SetCVar(CVars.AudioDefaultConcurrent, MinimumConcurrentSounds);
    }
}
