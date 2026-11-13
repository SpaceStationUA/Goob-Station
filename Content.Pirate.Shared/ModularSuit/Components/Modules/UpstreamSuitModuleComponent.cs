namespace Content.Pirate.Shared.ModularSuit;

/// <summary>Runs an upstream module's effects on a Pirate modular suit.</summary>
[RegisterComponent]
public sealed partial class UpstreamSuitModuleComponent : Component
{
    public EntityUid? LastWearer;
}
