namespace Content.Shared._DV.Psionics.Events;

/// <summary>
/// Event that gets raised whenever an entity first gains a psionic power,
/// carrying the localized feedback text meant to be shown to the player who gained it.
/// </summary>
/// <param name="user">The entity that gained the power.</param>
/// <param name="feedback">The localized power-gain feedback message.</param>
public sealed class PsionicPowerGainedEvent(EntityUid user, string feedback) : EntityEventArgs
{
    public EntityUid User = user;
    public string Feedback = feedback;
}
