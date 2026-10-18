// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Client.Lobby;
using Content.Shared._Pirate.StationRecords;
using Content.Shared.Roles;
using Robust.Client.UserInterface;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.StationRecords;

public sealed class RecordPortraitRequestSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly RecordPhotoComposerSystem _composer = default!;

    private const int Attempts = 4;

    private readonly HashSet<EntityUid> _pendingDummies = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RecordPortraitRenderRequestEvent>(OnRenderRequest);
        SubscribeLocalEvent<LocalPlayerDetachedEvent>(OnLocalPlayerDetached);
    }

    // Headless renders may never finish, so delete pending dummies when the player detaches.
    private void OnLocalPlayerDetached(LocalPlayerDetachedEvent ev)
    {
        foreach (var dummy in _pendingDummies)
        {
            if (Exists(dummy))
                Del(dummy);
        }

        _pendingDummies.Clear();
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
            _pendingDummies.Add(dummy);
            _composer.DressForPhoto(dummy);

            byte[]? image = null;
            for (var attempt = 0; attempt < Attempts && image is not { Length: > 0 } && Exists(dummy); attempt++)
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
        catch (OperationCanceledException)
        {
            // The composer cancels pending renders when the client shuts down.
        }
        catch (Exception e)
        {
            Log.Warning($"Failed to render the arrival record photo: {e}");
        }
        finally
        {
            _pendingDummies.Remove(dummy);
            if (dummy.IsValid() && Exists(dummy))
                Del(dummy);
        }
    }
}
