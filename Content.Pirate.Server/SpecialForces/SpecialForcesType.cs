using System;
using System.Collections.Generic;
using System.Linq;

namespace Content.Pirate.Server.SpecialForces;

public enum SpecialForcesType
{
    ERT = 0,
    DeathSquad = 1,
    CBURN = 2,
    HecuHuman = 3,
    HecuIpc = 4
}

/// <summary>Provides dashed command aliases for HECU groups; other values use their enum names.</summary>
public static class SpecialForcesTypeNames
{
    private static readonly Dictionary<SpecialForcesType, string> CommandNames = new()
    {
        { SpecialForcesType.HecuHuman, "HECU-human" },
        { SpecialForcesType.HecuIpc, "HECU-ipc" },
    };

    public static string ToCommandName(this SpecialForcesType type)
    {
        return CommandNames.TryGetValue(type, out var name) ? name : type.ToString();
    }

    public static IEnumerable<string> AllCommandNames()
    {
        return Enum.GetValues<SpecialForcesType>().Select(ToCommandName);
    }

    public static bool TryParse(string input, out SpecialForcesType type)
    {
        foreach (var (value, name) in CommandNames)
        {
            if (!string.Equals(name, input, StringComparison.OrdinalIgnoreCase))
                continue;

            type = value;
            return true;
        }

        foreach (var value in Enum.GetValues<SpecialForcesType>())
        {
            if (!string.Equals(value.ToString(), input, StringComparison.OrdinalIgnoreCase))
                continue;

            type = value;
            return true;
        }

        type = default;
        return false;
    }
}
