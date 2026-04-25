using System.Numerics;

namespace Content.Shared._Pirate.Coffeemaker.Components;

[RegisterComponent]
public sealed partial class CoffeemakerSteamPuffComponent : Component
{
    [DataField]
    public Vector2 Velocity = new(0f, 0.2f);
}
