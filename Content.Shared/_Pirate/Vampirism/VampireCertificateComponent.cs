using Content.Shared.Preferences;
using Robust.Shared.GameStates;

namespace Content.Shared._Pirate.Vampirism;

/// <summary>
/// A document that confirms its holder is an officially registered vampire.
/// Behaves like a passport: it can be opened and closed in hand.
/// </summary>
[RegisterComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true), NetworkedComponent]
public sealed partial class VampireCertificateComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool IsClosed = true;

    /// <summary>
    /// Name of the vampire this certificate was issued to.
    /// </summary>
    [DataField]
    public string OwnerName = string.Empty;

    /// <summary>
    /// Profile of the vampire this certificate was issued to, used to print race and gender.
    /// </summary>
    [ViewVariables]
    public HumanoidCharacterProfile? OwnerProfile;

    /// <summary>
    /// Passport ID of the owner, matching the one printed on their passport.
    /// </summary>
    [DataField]
    public string PassportId = string.Empty;

    [DataField, AutoNetworkedField]
    public string ClosedState = "document_closed";

    [DataField, AutoNetworkedField]
    public string OpenState = "document_opened";
}
