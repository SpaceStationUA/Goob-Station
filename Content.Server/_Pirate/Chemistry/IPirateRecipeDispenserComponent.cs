using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;

namespace Content.Server._Pirate.Chemistry;

public interface IPirateRecipeDispenserComponent
{
    Dictionary<string, Dictionary<string, FixedPoint2>> SavedRecipes { get; }
    Dictionary<string, FixedPoint2>? RecordingRecipe { get; set; }
    SoundSpecifier ClickSound { get; }
    SoundSpecifier ErrorSound { get; }
}
