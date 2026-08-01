// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Heretic.Lock;

[Serializable, NetSerializable]
public sealed class EldritchIdMessage(EldritchIdConfiguration config) : BoundUserInterfaceMessage
{
    public EldritchIdConfiguration Config = config;
}
[Serializable, NetSerializable]
public enum EldritchIdUiKey : byte
{
    Key
}
