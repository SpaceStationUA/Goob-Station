using System.Numerics;
using Content.Pirate.Shared.Overlays.Shockwave;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Pirate.Client.Overlays.Shockwave;

public sealed partial class ShockwaveOverlay : Robust.Client.Graphics.Overlay
{
    private static readonly ProtoId<ShaderPrototype> ShockwaveShader = "ShockwaveShader";
    private const int InstanceLimit = 10;

    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SharedTransformSystem _transformSystem;
    private readonly ShaderInstance _shader;
    private readonly ShaderArgs _shaderArgs = new(InstanceLimit);

    private float _distortionScale = 1.0f;

    public override bool RequestScreenTexture => true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    public ShockwaveOverlay()
    {
        ZIndex = 8;

        IoCManager.InjectDependencies(this);
        _shader = _prototypeManager.Index(ShockwaveShader).InstanceUnique();
        _transformSystem = _entityManager.System<SharedTransformSystem>();
        _configManager.OnValueChanged(CCVars.ReducedMotion, OnReducedMotionChanged, invokeImmediately: true);
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return false;

        _shaderArgs.Clear();

        var enumerator = _entityManager.AllEntityQueryEnumerator<ShockwaveComponent, TransformComponent>();
        while (enumerator.MoveNext(out var shockwave, out var transform))
        {
            if (transform.MapID != args.MapId)
                continue;

            var screenCoords = args.Viewport.WorldToLocal(_transformSystem.GetWorldPosition(transform));
            screenCoords.X /= args.Viewport.Size.X;
            screenCoords.Y = 1 - screenCoords.Y / args.Viewport.Size.Y;

            _shaderArgs.Append(
                screenCoords,
                shockwave.Intensity,
                shockwave.Width * _distortionScale,
                shockwave.FallOff,
                shockwave.PowerFactor * _distortionScale,
                (float) shockwave.StartTime.TotalSeconds,
                shockwave.TimeScale);

            if (_shaderArgs.Count >= InstanceLimit)
                break;
        }

        return _shaderArgs.Count > 0;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (ScreenTexture == null || args.Viewport.Eye == null || _shaderArgs.Count == 0)
            return;

        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("RENDER_SCALE", args.Viewport.Eye.Scale);
        _shader.SetParameter("CURR_TIME", (float) _timing.CurTime.TotalSeconds);

        _shaderArgs.SetShaderParams(_shader);

        var worldHandle = args.WorldHandle;
        worldHandle.UseShader(_shader);
        worldHandle.DrawRect(args.WorldBounds, Color.White);
        worldHandle.UseShader(null);
    }

    private void OnReducedMotionChanged(bool reducedMotion)
    {
        _distortionScale = reducedMotion ? 0.01f : 1f;
    }

    private sealed class ShaderArgs
    {
        private readonly Vector2[] _epicenters;
        private readonly float[] _intensities;
        private readonly float[] _widths;
        private readonly float[] _fallOffs;
        private readonly float[] _powerFactors;
        private readonly float[] _startTimes;
        private readonly float[] _timeScales;

        public int Count { get; private set; }

        public ShaderArgs(int instanceLimit)
        {
            _epicenters = new Vector2[instanceLimit];
            _intensities = new float[instanceLimit];
            _widths = new float[instanceLimit];
            _fallOffs = new float[instanceLimit];
            _powerFactors = new float[instanceLimit];
            _startTimes = new float[instanceLimit];
            _timeScales = new float[instanceLimit];
        }

        public void Clear()
        {
            Count = 0;
        }

        public void Append(
            Vector2 epicenter,
            float intensity,
            float width,
            float fallOff,
            float powerFactor,
            float startTime,
            float timeScale)
        {
            _epicenters[Count] = epicenter;
            _intensities[Count] = intensity;
            _widths[Count] = width;
            _fallOffs[Count] = fallOff;
            _powerFactors[Count] = powerFactor;
            _startTimes[Count] = startTime;
            _timeScales[Count] = timeScale;
            Count++;
        }

        public void SetShaderParams(ShaderInstance shader)
        {
            shader.SetParameter("EPICENTERS", _epicenters);
            shader.SetParameter("INTENSITIES", _intensities);
            shader.SetParameter("WIDTHS", _widths);
            shader.SetParameter("FALLOFFS", _fallOffs);
            shader.SetParameter("START_TIMES", _startTimes);
            shader.SetParameter("POWER_FACTORS", _powerFactors);
            shader.SetParameter("TIME_SCALES", _timeScales);
            shader.SetParameter("COUNT", Count);
        }
    }
}
