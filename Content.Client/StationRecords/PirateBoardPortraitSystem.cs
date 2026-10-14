// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Lobby;
using Content.Client._Pirate.StationRecords; // Pirate: records photos
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

// Pirate: records photos - Render a profile dummy, never a live crew mob.
public sealed class PirateBoardPortraitSystem : EntitySystem
{
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly RecordPhotoComposerSystem _composer = default!; // Pirate: records photos

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
                _composer.DressForPhoto(puppet); // Pirate: records photos
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

    // Pirate: records photos - Keep renders in memory; sprite export writes PNGs to the player's data folder.
    private async Task<byte[]?> TryGeneratePortraitData(EntityUid puppet, int attempts = 4)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            try
            {
                if (await _composer.RenderEntity(puppet) is { Length: > 0 } data) // Pirate: records photos
                    return data;
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
