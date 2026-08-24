// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._Pirate.Temperature;
using Content.Shared.Clothing;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Temperature;

public sealed class BlackBodyVisualizerSystem : VisualizerSystem<BlackBodyComponent>
{
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    private EntityQuery<PointLightComponent> _lightQuery;
    private EntityQuery<SpriteComponent> _spriteQuery;

    public static readonly ProtoId<ShaderPrototype> EmissiveShader = "Emissive";
    public const float MinGlowTemp = 600f;
    public const float Planck = 6.62607004e-34f;
    public const float StephanBoltzmann = 5.670373e-8f;
    public const float Boltzmann = 1.3806485279e-23f;
    public const float SpeedOfLight = 299792458f;
    public const float Gamma = 1f / 2.2f;

    public override void Initialize()
    {
        base.Initialize();

        _lightQuery = GetEntityQuery<PointLightComponent>();
        _spriteQuery = GetEntityQuery<SpriteComponent>();
        SubscribeLocalEvent<BlackBodyComponent, HeldVisualsUpdatedEvent>(OnHeldVisualsUpdated);
        SubscribeLocalEvent<BlackBodyComponent, EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
    }

    protected override void OnAppearanceChange(
        EntityUid uid,
        BlackBodyComponent component,
        ref AppearanceChangeEvent args)
    {
        if (!_spriteQuery.TryComp(uid, out var sprite) ||
            !AppearanceSystem.TryGetData<float>(
                uid,
                BlackBodyVisuals.Temperature,
                out var temperature,
                args.Component))
        {
            return;
        }

        var color = GetEmissiveColor(temperature);
        if (component.Color == color)
            return;

        component.Color = color;
        foreach (var layer in sprite.AllLayers)
            SetLayerEmissive((SpriteComponent.Layer) layer, color);

        _item.VisualsChanged(uid);

        if (!_lightQuery.TryComp(uid, out var light))
            return;

        var glowing = temperature > MinGlowTemp;
        _light.SetEnabled(uid, glowing, light);
        if (!glowing)
            return;

        var energy = MathF.Pow(temperature / 400f, 1.5f);
        var radius = 1.25f + color.A * component.MaxLightRadius;
        _light.SetColor(uid, color, light);
        _light.SetEnergy(uid, energy, light);
        _light.SetRadius(uid, radius, light);
    }

    private void OnHeldVisualsUpdated(Entity<BlackBodyComponent> ent, ref HeldVisualsUpdatedEvent args)
    {
        UpdateLayers(args.User, ent.Comp.Color, args.RevealedLayers);
    }

    private void OnEquipmentVisualsUpdated(Entity<BlackBodyComponent> ent, ref EquipmentVisualsUpdatedEvent args)
    {
        UpdateLayers(args.Equipee, ent.Comp.Color, args.RevealedLayers);
    }

    private void UpdateLayers(EntityUid uid, Color color, HashSet<string> keys)
    {
        if (!_spriteQuery.TryComp(uid, out var sprite))
            return;

        var entity = (uid, sprite);
        foreach (var key in keys)
        {
            if (SpriteSystem.LayerMapTryGet(entity, key, out var index, true) &&
                SpriteSystem.TryGetLayer(entity, index, out var layer, true))
            {
                SetLayerEmissive(layer, color);
            }
        }
    }

    private static void SetLayerEmissive(SpriteComponent.Layer layer, Color color)
    {
        if (layer.ShaderPrototype != EmissiveShader || layer.Shader is not { } shader)
            return;

        if (!shader.Mutable)
        {
            shader = shader.Duplicate();
            layer.Shader = shader;
        }

        shader.SetParameter("emissive", color);
    }

    public static Color GetEmissiveColor(float temperature)
    {
        if (temperature < MinGlowTemp)
            return Color.Transparent;

        temperature = Math.Clamp(temperature, MinGlowTemp, 6000f);
        var flux = BlackBodyFlux(temperature);
        var redUpper = WavelengthValue(700e-9f, temperature);
        var redLower = WavelengthValue(600e-9f, temperature);
        var greenLower = WavelengthValue(500e-9f, temperature);
        var blueLower = WavelengthValue(400e-9f, temperature);

        var red = flux * (redUpper - redLower);
        var green = flux * (redLower - greenLower);
        var blue = flux * (greenLower - blueLower);
        Correct(ref red);
        Correct(ref green);
        Correct(ref blue);

        var kiloKelvin = temperature * 0.001f;
        var alpha = Math.Clamp(
            MathF.Log(1.347f * kiloKelvin) - 0.118f * kiloKelvin + 0.313f,
            0f,
            1f);
        return new Color(red, green, blue, alpha);
    }

    private static void Correct(ref float channel)
    {
        channel = 1f - MathF.Pow(2, -channel);
        channel = MathF.Pow(channel, Gamma);
    }

    private static float BlackBodyFlux(float temperature)
        => StephanBoltzmann * MathF.Pow(temperature, 4);

    private static float WavelengthValue(float wavelength, float temperature)
    {
        const float scale = 15f / (MathF.PI * MathF.PI * MathF.PI * MathF.PI);
        const float c2 = Planck * SpeedOfLight / Boltzmann;
        var z = c2 / (wavelength * temperature);
        var zSquared = z * z;
        return scale * (z * zSquared + 3f * zSquared + 6f * z + 6f) * MathF.Exp(-z);
    }
}
