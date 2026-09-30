// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Lobby;
using Content.Client.Sprite;
using Content.Pirate.Shared.WebUi;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using System.IO;
using System.Threading.Tasks;

namespace Content.Client.StationRecords;

/// <summary>
///     Portraits for the evidence board: when the server quests a pinned
///     character card, this renders the spawn-time profile snapshot the
///     same way the CriminalRecords console does — LoadProfileEntity
///     dummy, ContentSpriteSystem.Export to a PNG, upload back — and the
///     server paints it as a data URI. Never touches a live crew mob.
/// </summary>
public sealed class PirateBoardPortraitSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly IResourceManager _res = default!;
    [Dependency] private readonly ContentSpriteSystem _sprites = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<Content.Pirate.Shared.WebUi.EvidenceBoardPortraitQuestEvent>(OnQuest);
    }

    private async void OnQuest(Content.Pirate.Shared.WebUi.EvidenceBoardPortraitQuestEvent msg)
    {
        var profile = msg.Profile;
        if (profile == null)
            return;

        try
        {
            Robust.Shared.GameObjects.EntityUid puppet;
            try
            {
                JobPrototype? job = null;
                if (!string.IsNullOrWhiteSpace(msg.JobProto) &&
                    _protos.TryIndex(msg.JobProto, out JobPrototype? indexedJob) &&
                    indexedJob != null)
                {
                    job = indexedJob;
                }
                puppet = _ui.GetUIController<LobbyUIController>()
                    .LoadProfileEntity(profile, job, true);
            }
            catch (Exception ex)
            {
                Logger.WarningS("webui.board", $"portrait dummy failed: {ex.Message}");
                return;
            }

            var data = await TryGeneratePortraitData(puppet);
            _ent.DeleteEntity(puppet);

            if (data is not { Length: > 32 })
                return;

            IoCManager.Resolve<IEntityNetworkManager>().SendSystemNetworkMessage(
                new Content.Pirate.Shared.WebUi.EvidenceBoardRequestEvent
                {
                    Console = msg.Console,
                    Action = "portrait",
                    Data = msg.CardId.ToString(),
                    Image = data,
                });
        }
        catch (Exception ex)
        {
            Logger.WarningS("webui.board", $"portrait render failed: {ex.Message}");
        }
    }

    // Mirrors CriminalRecordsConsoleWindow.TryGeneratePortraitSnapshotImageData
    // (Export -> user-data PNG -> bytes, with small retry).
    private async Task<byte[]?> TryGeneratePortraitData(EntityUid puppet, int attempts = 4)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                var metadata = _ent.GetComponent<MetaDataComponent>(puppet);
                var filePath = ContentSpriteSystem.Exports /
                    $"{metadata.EntityName}-{Direction.South}-{puppet}.png";

                await _sprites.Export(puppet, Direction.South, includeId: true);

                if (!_res.UserData.Exists(filePath))
                {
                    await Task.Delay(100);
                    continue;
                }

                await using var file = _res.UserData.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var imageData = new MemoryStream();
                await file.CopyToAsync(imageData);
                if (imageData.Length > 0)
                    return imageData.ToArray();
            }
            catch (Exception)
            {
                // fall through to retry
            }
            await Task.Delay(100);
        }
        return null;
    }
}
