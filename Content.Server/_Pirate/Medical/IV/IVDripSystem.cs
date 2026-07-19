using System.Diagnostics.CodeAnalysis;
using Content.Server.Chat.Systems;
using Content.Shared._Pirate.Medical.IV;
using Content.Shared.Body.Components;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Medical.IV;

public sealed class IVDripSystem : SharedIVDripSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private bool TryGetBloodstream(
        EntityUid attachedTo,
        [NotNullWhen(true)] out Entity<SolutionComponent>? solEnt,
        [NotNullWhen(true)] out Solution? solution)
    {
        solEnt = default;
        solution = default;

        if (!TryComp(attachedTo, out BloodstreamComponent? attachedStream) ||
            !_solutionContainer.TryGetSolution(attachedTo, attachedStream.BloodSolutionName, out solEnt, out solution))
        {
            return false;
        }

        return true;
    }

    protected override void DoRip(DamageSpecifier? damage,
        EntityUid attached,
        EntityUid? user,
        ProtoId<EmotePrototype> ripEmote,
        bool predict)
    {
        base.DoRip(damage, attached, user, ripEmote, predict);
        _chat.TryEmoteWithoutChat(attached, ripEmote.Id);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var time = _timing.CurTime;
        var ivs = EntityQueryEnumerator<IVDripComponent>();
        while (ivs.MoveNext(out var ivId, out var ivComp))
        {
            if (ivComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(ivId, attachedTo, ivComp.Range))
                DetachIV((ivId, ivComp), null, true, false);

            if (time < ivComp.TransferAt)
                continue;

            if (_itemSlots.GetItemOrNull(ivId, ivComp.Slot) is not { } pack)
                continue;

            if (!TryComp(pack, out BloodPackComponent? packComponent))
                continue;

            ivComp.TransferAt = time + ivComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(pack, packComponent.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol))
                continue;

            if (ivComp.Injecting)
            {
                if (streamSol.Volume < streamSol.MaxVolume)
                {
                    var excludedSolution = packSol.SplitSolutionWithout(packSol.MaxVolume, packComponent.TransferableReagents);
                    _solutionContainer.TryTransferSolution(streamSolEnt.Value, packSol, ivComp.TransferAmount);
                    _solutionContainer.TryAddSolution(packSolEnt.Value, excludedSolution);
                    Dirty(packSolEnt.Value);
                }
            }
            else if (packSol.Volume < packSol.MaxVolume)
            {
                _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, ivComp.TransferAmount);
                Dirty(streamSolEnt.Value);
            }

            Dirty(ivId, ivComp);
            UpdateIVVisuals((ivId, ivComp));
            UpdatePackVisuals((pack, packComponent));
        }

        var packs = EntityQueryEnumerator<BloodPackComponent>();
        while (packs.MoveNext(out var packId, out var packComp))
        {
            if (packComp.AttachedTo is not { } attachedTo)
                continue;

            if (!InRange(packId, attachedTo, packComp.Range))
                DetachPack((packId, packComp), null, true, false);

            if (time < packComp.TransferAt)
                continue;

            packComp.TransferAt = time + packComp.TransferDelay;

            if (!_solutionContainer.TryGetSolution(packId, packComp.Solution, out var packSolEnt, out var packSol))
                continue;

            if (!TryGetBloodstream(attachedTo, out var streamSolEnt, out var streamSol))
                continue;

            if (packComp.Injecting)
            {
                if (streamSol.Volume < streamSol.MaxVolume)
                {
                    var excludedSolution = packSol.SplitSolutionWithout(packSol.MaxVolume, packComp.TransferableReagents);
                    _solutionContainer.TryTransferSolution(streamSolEnt.Value, packSol, packComp.TransferAmount);
                    _solutionContainer.TryAddSolution(packSolEnt.Value, excludedSolution);
                    Dirty(packSolEnt.Value);
                }
            }
            else if (packSol.Volume < packSol.MaxVolume)
            {
                _solutionContainer.TryTransferSolution(packSolEnt.Value, streamSol, packComp.TransferAmount);
                Dirty(streamSolEnt.Value);
            }

            Dirty(packId, packComp);
            UpdatePackVisuals((packId, packComp));
        }
    }
}
