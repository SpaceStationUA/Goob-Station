// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Light;

public sealed class StarlightMapSystem : EntitySystem
{
    public void SetStarlight(EntityUid mapUid, Color? color, float? strength)
    {
        var comp = EnsureComp<StarlightComponent>(mapUid);
        comp.Color = color;
        comp.Strength = strength is { } s ? Math.Clamp(s, 0f, 1f) : null;
        Dirty(mapUid, comp);
    }

    public void ClearStarlight(EntityUid mapUid)
    {
        RemComp<StarlightComponent>(mapUid);
    }
}
