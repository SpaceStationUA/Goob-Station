using Content.Shared.Forensics.Components;
using Content.Shared.Popups;
using Robust.Shared.Audio; // Pirate: MODsuit action fixes
using Robust.Shared.Audio.Systems;

namespace Content.Goobstation.Shared.ModSuits.Modules;

public sealed partial class ModDnaLockModuleSystem : EntitySystem
{
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [SubscribeLocalEvent]
    private void OnAccessAttempt(Entity<ModDnaLockModuleComponent> ent, ref ModSuitAccessAttemptEvent args)
    {
        if (ent.Comp.LockedDna is { } locked && GetDna(args.User) != locked)
            args.Cancelled = true;
    }

    [SubscribeLocalEvent]
    private void OnUsed(Entity<ModDnaLockModuleComponent> ent, ref ModModuleUsedEvent args)
    {
        args.Handled = true;

        if (ent.Comp.LockedDna != null)
        {
            ent.Comp.LockedDna = null;
            Dirty(ent);
            SendFeedback(args, "modsuit-dna-unlocked", ent.Comp.Sound); // Pirate: MODsuit action fixes
            return;
        }

        if (GetDna(args.User) is not { } dna)
        {
            SendFeedback(args, "modsuit-dna-none"); // Pirate: MODsuit action fixes
            return;
        }

        ent.Comp.LockedDna = dna;
        Dirty(ent);
        SendFeedback(args, "modsuit-dna-locked", ent.Comp.Sound); // Pirate: MODsuit action fixes
    }

    #region Pirate: MODsuit action fixes
    private void SendFeedback(ModModuleUsedEvent args, LocId message, SoundSpecifier? sound = null)
    {
        var text = Loc.GetString(message);
        if (args.ServerOnly)
        {
            _audio.PlayPvs(sound, args.User);
            _popup.PopupEntity(text, args.User, args.User);
            return;
        }

        _audio.PlayPredicted(sound, args.User, args.User);
        _popup.PopupClient(text, args.User, args.User);
    }
    #endregion

    private string? GetDna(EntityUid user)
    {
        return TryComp<DnaComponent>(user, out var dna) ? dna.DNA : null;
    }
}
