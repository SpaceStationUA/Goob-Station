// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Security;

/// <summary>
/// Status used in Criminal Records.
///
/// None - the default value
/// Suspected - the person is suspected of doing something illegal
/// Wanted - the person is being wanted by security
/// Hostile - the person has been admitted as hostile
/// Detained - the person is detained by security
/// Paroled - the person is on parole
/// Discharged - the person has been released from prison
/// Eliminated - the person has been eliminated and should not be healed
/// Search - the person needs to be searched
/// Perma - the person has been sentenced to permanent imprisonment
/// Dangerous - the person is highly dangerous and may resist arrest
/// Demote - The person is about to be demoted
/// Warned - The person was let off with a warning instead of a punishment (Pirate)
/// Safe - The person was searched and found clean (Pirate)
/// </summary>
public enum SecurityStatus : byte
{
    None,
    Suspected,
    Wanted,
    Hostile,
    Detained,
    Paroled,
    Discharged,
    Eliminated,
    Search, // Goobstation
    Perma, // Goobstation
    Dangerous, // Goobstation
    Demote, // Goobstation - Demotion
    Warned, // Pirate - warning issued instead of a punishment
    Safe // Pirate - searched and found clean
}
