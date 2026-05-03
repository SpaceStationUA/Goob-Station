// SPDX-FileCopyrightText: 2026 Corvax Team Contributors
// SPDX-FileCopyrightText: 2026 CyberLanos <cyber.lanos00@gmail.com>
//
// Ported in part from Space Station 14.
// Original SPDX-License-Identifier: AGPL-3.0-or-later
// SPDX-License-Identifier: AGPL-3.0-only

using Robust.Shared.Audio;
using Robust.Shared.Serialization;
using System.Collections.Generic;
using System.Numerics;

namespace Content.Shared._Pirate.Photo;

[RegisterComponent]
public sealed partial class PhotoCameraComponent : Component
{
    [DataField]
    public Vector2 ViewBox = new Vector2(20, 20);
    [DataField]
    public float MinZoom = 0.1f, MaxZoom = 1f;

    [DataField]
    public SoundSpecifier PhotoSound = new SoundCollectionSpecifier("PhotoCameraShutter");
    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/airlock_deny.ogg");
    [DataField]
    public SoundSpecifier InsertSound = new SoundPathSpecifier("/Audio/Machines/id_insert.ogg");

    [DataField]
    public string CardPrototype = "PhotoCard";
    [DataField]
    public string CardMaterial = "Paper";
    [DataField]
    public int CardCost = 100;

    [ViewVariables]
    public HashSet<EntityUid> OpenUsers = new();
}

[Serializable, NetSerializable]
public sealed class PhotoCameraUiState : BoundUserInterfaceState
{
    public NetEntity CameraEntity { get; }

    public bool HasPaper { get; }

    public PhotoCameraUiState(NetEntity cameraEntity, bool hasPaper)
    {
        CameraEntity = cameraEntity;
        HasPaper = hasPaper;
    }
}

[Serializable, NetSerializable]
public enum PhotoCameraUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class PhotoCameraTakeImageMessage : BoundUserInterfaceMessage
{
    public byte[] Data { get; }
    public byte[]? PreviewData { get; }
    public float Zoom { get; }
    public IReadOnlyList<NetEntity> CapturedEntities { get; }

    public PhotoCameraTakeImageMessage(byte[] data, byte[]? previewData, float zoom, IReadOnlyList<NetEntity> capturedEntities)
    {
        Data = (byte[]) data.Clone();
        PreviewData = previewData == null ? null : (byte[]) previewData.Clone();
        Zoom = zoom;
        var copiedEntities = new NetEntity[capturedEntities.Count];
        for (var i = 0; i < capturedEntities.Count; i++)
        {
            copiedEntities[i] = capturedEntities[i];
        }

        CapturedEntities = copiedEntities;
    }
}


