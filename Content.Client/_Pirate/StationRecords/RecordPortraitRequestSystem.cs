// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Client.Lobby;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordPortraitRequestSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly RecordPhotoComposerSystem _composer = default!;

    private const int Attempts = 4;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RecordPortraitRenderRequestEvent>(OnRenderRequest);
    }

    private async void OnRenderRequest(RecordPortraitRenderRequestEvent ev)
    {
        var dummy = EntityUid.Invalid;
        try
        {
            JobPrototype? job = null;
            if (!string.IsNullOrWhiteSpace(ev.JobPrototype) && _proto.TryIndex<JobPrototype>(ev.JobPrototype, out var indexedJob))
                job = indexedJob;

            dummy = _ui.GetUIController<LobbyUIController>().LoadProfileEntity(ev.Profile, job, true);
            _composer.DressForPhoto(dummy);

            byte[]? image = null;
            for (var attempt = 0; attempt < Attempts && image is not { Length: > 0 }; attempt++)
            {
                if (attempt > 0)
                    await Task.Delay(100);

                image = await _composer.RenderEntity(dummy);
            }

            if (image is { Length: > 0 })
            {
                RaiseNetworkEvent(new RecordPortraitRenderResultEvent
                {
                    Station = ev.Station,
                    RecordId = ev.RecordId,
                    ImageData = image,
                });
            }
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to render the arrival record photo: {e}");
        }
        finally
        {
            if (dummy.IsValid() && Exists(dummy))
                Del(dummy);
        }
    }
}
