using System.Numerics;
using System.Text;
using Content.Shared._FarHorizons.StarSystem.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._FarHorizons.StarSystem.Helpers;

[DataDefinition]
public sealed partial class Star
{
    [ViewVariables] public float SolarMass;
    [ViewVariables] public float Luminosity;
    [ViewVariables] public float Radius;
    [ViewVariables] public float Temperature;
    [ViewVariables] public string Shader;
    [ViewVariables(VVAccess.ReadWrite)] public Color Color;
    [ViewVariables] public Vector2 Position;
    [ViewVariables] public string Name = "";
    [ViewVariables] public float Rotation;
    [ViewVariables] public PlanetaryRings? Rings;
    public const float NAV_PIXEL_SIZE = 500;
    public const float MAP_PIXEL_SIZE = 500;
    public const string STAR_ENTITY = "StarEntity";

    private const string UppercaseLetters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public Star()
    {
        Color = Color.White;
        Shader = string.Empty;
    }

    public Star(float solarMass, Color color, string shader)
    {
        SolarMass = Math.Clamp(solarMass, 0.08f, 8.0f); // Main Sequence stars

        Luminosity = SolarMass < 0.43f
            ? 0.23f * MathF.Pow(SolarMass, 2.3f)
            : SolarMass < 2.0f ? MathF.Pow(SolarMass, 4.0f) : 1.4f * MathF.Pow(SolarMass, 3.5f);

        Radius = SolarMass < 1.66f ? MathF.Pow(SolarMass, 0.9f) : 1.15f * MathF.Pow(SolarMass, 0.6f);

        Temperature = MathF.Pow(Luminosity / MathF.Pow(Radius, 2f), 0.25f) * 5778f;

        Color = color;
        Position = Vector2.Zero;
        Shader = shader;
    }

    /// <summary>
    /// Builds a star from its type prototype: rolls the mass range, honours a fixed curated
    /// name (Kyphrus), and attaches a ring system when the type defines one.
    /// </summary>
    public Star(StarTypePrototype proto, System.Random rand, IPrototypeManager protoMan)
        : this(proto.SolarMass.RollValue(rand), proto.Color, proto.Shader)
    {
        Name = proto.Name ?? "";
        Rotation = proto.Rotation;

        if (proto.Rings is { } rings)
            Rings = new PlanetaryRings(rand, protoMan, rings);
    }

    public void GenerateName(System.Random rand)
    {
        var letterCount = rand.Next(0, 2) == 0 ? 2 : 3;

        var sb = new StringBuilder();

        for (var i = 0; i < letterCount; i++)
        {
            var letter = UppercaseLetters[rand.Next(UppercaseLetters.Length)];
            sb.Append(letter);
        }

        sb.Append('-');

        var number = rand.Next(100, 999);
        sb.Append(number);

        sb.Append('-');

        var suffix = UppercaseLetters[rand.Next(UppercaseLetters.Length)];
        sb.Append(suffix);

        Name = sb.ToString();
    }

    public string GetPlanetName(int order) => 
        $"{Name} {ToRomanNumeral(order)}";
    
    // Zero-based: order 0 is "I", 1 is "II", and so on. Algorithmic so orders
    // beyond 19 keep producing valid numerals instead of "??".
    // (No collection expressions here: they compile to InlineArray helpers that the
    // engine's ILVerify step rejects.)
    private static readonly int[] RomanValues = new[] { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
    private static readonly string[] RomanNumerals = new[] { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

    private string ToRomanNumeral(int order)
    {
        var value = order + 1;

        var sb = new StringBuilder();
        for (var i = 0; i < RomanValues.Length; i++)
        {
            while (value >= RomanValues[i])
            {
                value -= RomanValues[i];
                sb.Append(RomanNumerals[i]);
            }
        }

        return sb.ToString();
    }
}
