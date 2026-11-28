// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._Pirate.CaptureTheFlag;

[Serializable, NetSerializable]
public enum CaptureTheFlagRelayState : byte
{
    Neutral,
    Red,
    Blue,
    TakingRed,
    TakingBlue,
    Contested,
}

[Serializable, NetSerializable]
public sealed class CaptureTheFlagScoreEvent(int red, int blue, int tier, CaptureTheFlagRelayState center,
    int redReserve, int blueReserve) : EntityEventArgs
{
    public readonly int Red = red;
    public readonly int Blue = blue;
    public readonly int Tier = tier;
    public readonly CaptureTheFlagRelayState Center = center;
    public readonly int RedReserve = redReserve;
    public readonly int BlueReserve = blueReserve;
}

[Serializable, NetSerializable]
public sealed class CaptureTheFlagScoreClosedEvent : EntityEventArgs;

[Serializable, NetSerializable]
public sealed class CaptureTheFlagAssignmentEvent(bool? red) : EntityEventArgs
{
    public readonly bool? Red = red;
}
