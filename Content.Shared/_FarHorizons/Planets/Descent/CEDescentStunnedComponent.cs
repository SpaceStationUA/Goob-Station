/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using Robust.Shared.GameStates;

namespace Content.Shared._FarHorizons.Planets.Descent;

/// <summary>
/// The descent drive discharged violently — an engine took damage mid-charge — and has
/// to respool before another descent can begin. Applied to every grid of the aborted
/// charge's docked chain. Blocks <see cref="CESharedDescentSystem.TryBeginDescent"/>,
/// feeds the console's red "Respooling" state, and drives the client-side discharge
/// feedback (tapering radial screenshake + buzz) off <see cref="Start"/>. While stunned
/// the ship is dead in space: pilot input stays locked out and the
/// thrusters/gyroscopes are held cold. The server removes it once <see cref="End"/>
/// elapses.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEDescentStunnedComponent : Component
{
    /// <summary>When the discharge happened; the client shake tapers from here.</summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public TimeSpan Start;

    /// <summary>When the drive has finished respooling and descents unlock.</summary>
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public TimeSpan End;

    /// <summary>
    /// Server bookkeeping: whether THIS stun owns the grid's PreventPilot lock (either
    /// taken over from the aborted spinup or added fresh). Only then does the stun's
    /// shutdown hand the lock back — a grid locked for other reasons keeps its lock.
    /// </summary>
    [ViewVariables]
    public bool PilotLocked;
}
