/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.Serialization;

namespace Content.Shared._FarHorizons.Camera;

/// <summary>
/// Server -> client: flood the receiving player's screen with a solid colour that
/// instantly snaps to full strength and fades back out over <see cref="Duration"/>.
/// Purely cinematic — this deliberately bypasses the gameplay flash stack
/// (FlashableComponent, eye protection, stuns), so it works on any viewer and can't
/// be blocked by sunglasses. Handled by the client <c>CEScreenFlashSystem</c>,
/// which drives a fullscreen <c>CEScreenFlashOverlay</c>.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEScreenFlashEvent : EntityEventArgs
{
    /// <summary>Envelope length in seconds: instant flood, then ease back to nothing.</summary>
    public float Duration = 1f;

    /// <summary>Flood colour; the alpha channel scales the peak opacity.</summary>
    public Color Color = Color.White;
}
