// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Pirate.Server.Silicons.Borgs;

/// <summary>
/// Shared alert-level policy for security cyborg access and lethal weapon modes.
/// </summary>
public static class SecurityBorgAlertLevelPolicy
{
    public static SecurityBorgAlertLevelTier GetTier(string? alertLevel)
    {
        return alertLevel?.ToLowerInvariant() switch
        {
            null or "" or "green" => SecurityBorgAlertLevelTier.Green,
            "red" or "violet" or "gamma" or "delta" or "epsilon" or "amber" or "octarine" =>
                SecurityBorgAlertLevelTier.Full,
            _ => SecurityBorgAlertLevelTier.Officer,
        };
    }
}

public enum SecurityBorgAlertLevelTier
{
    Green,
    Officer,
    Full,
}
