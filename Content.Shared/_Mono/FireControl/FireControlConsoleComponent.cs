namespace Content.Shared._Mono.FireControl;

/// <summary>
/// These are for the consoles that provide the user interface for fire control servers.
/// </summary>
[RegisterComponent]
public sealed partial class FireControlConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? ConnectedServer = null;

    #region Pirate: multiz
    /// <summary>
    /// The z-depth this console is currently focused on. Persistent across BUI state pushes so
    /// the operator's tab choice is preserved. <c>null</c> means "use the console's own depth".
    /// </summary>
    [ViewVariables]
    public int? SelectedLayerDepth = null;
    #endregion
}
