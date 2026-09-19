// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Audio.Jukebox;
using Content.Shared._Pirate.Audio.Jukebox;
using Content.Shared.Audio.Jukebox;

namespace Content.Client._Pirate.Audio.Jukebox;

public sealed class JukeboxRecordSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<JukeboxRecordsComponent, JukeboxRecordsChangedEvent>(OnRecordsChanged);
    }

    private void OnRecordsChanged(Entity<JukeboxRecordsComponent> ent, ref JukeboxRecordsChangedEvent args)
    {
        if (!_ui.TryGetOpenUi<JukeboxBoundUserInterface>(ent.Owner, JukeboxUiKey.Key, out var bui))
            return;

        bui.PopulateMusic();
        bui.Reload();
    }
}
