// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client._Pirate.PostProcess;
using Content.Client.Eye.Blinding;
using Content.Client.Flash;
using Content.Client.UserInterface.Systems.DamageOverlays.Overlays;
using Robust.Shared.Enums;

namespace Content.Client._Pirate.ZLevels.Core;

/// <summary>Moving blur to the final copy must not move it past another drawing overlay.</summary>
internal struct ZBlurFusionOrder
{
    private bool _sawBlur;
    public Type? Blocker { get; private set; }
    public readonly bool CanDefer => _sawBlur && Blocker == null;

    public bool Add(Type type, OverlaySpace space, bool knownInactive = false)
    {
        if ((space & OverlaySpace.WorldSpace) == 0)
            return false;
        if (type == typeof(CEZLevelBlurOverlay))
        {
            _sawBlur = true;
            return false;
        }
        if (!_sawBlur || knownInactive)
            return false;

        // These sealed implementations explicitly reject eyes other than the player's eye.
        // A crop always has a fresh synthetic ZEye. Recheck these guards after upstream changes.
        // Unknown overlays (including debug/vision effects) conservatively keep the old pass.
        if (type == typeof(BlindOverlay) || type == typeof(BlurryVisionOverlay) ||
            type == typeof(FlashOverlay) || type == typeof(DamageOverlay) ||
            type == typeof(CEPostProcessOverlay))
            return false;

        Blocker ??= type;
        return true;
    }
}
