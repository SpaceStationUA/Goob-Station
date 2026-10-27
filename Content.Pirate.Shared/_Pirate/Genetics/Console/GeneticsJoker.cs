// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Database;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Pirate.Shared.Genetics.Console;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, AutoGenerateComponentPause]
[Access(typeof(GeneticsConsoleSystem))]
public sealed partial class GeneticsJokerComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromMinutes(10);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan NextUse;
}

[Serializable, NetSerializable]
public sealed class GeneticsConsoleJokerMessage(uint sequence, uint index) : BoundUserInterfaceMessage
{
    public readonly uint Sequence = sequence;
    public readonly uint Index = index;

    // Rejects requests for a stale sequence list.
    public int? Number;
}

public sealed partial class GeneticsConsoleSystem
{
    private void InitializeJoker()
    {
        SubscribeLocalEvent<GeneticsJokerComponent, MapInitEvent>(OnJokerMapInit);
        Subs.BuiEvents<GeneticsJokerComponent>(GeneticsConsoleUiKey.Key, subs =>
        {
            subs.Event<GeneticsConsoleJokerMessage>(OnJoker);
        });
    }

    private void OnJokerMapInit(Entity<GeneticsJokerComponent> ent, ref MapInitEvent args)
    {
        // Rebuilding a console must not reset its cooldown.
        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);
    }

    private void OnJoker(Entity<GeneticsJokerComponent> ent, ref GeneticsConsoleJokerMessage args)
    {
        if (_net.IsClient)
            return;

        if (_timing.CurTime < ent.Comp.NextUse ||
            GetWorkableMob(ent.Owner) is not {} mob ||
            _genome.GetSequence(mob, args.Sequence) is not {} sequence ||
            _mutation.GetRoundData(sequence.Mutation) is not {} data ||
            args.Number is {} number && data.Number != number ||
            args.Index >= sequence.Bases.Length ||
            sequence.OriginalBases[(int) args.Index] != 'X' ||
            _mutation.IsDiscovered(ent, sequence.Mutation))
        {
            UpdateUI(ent.Owner);
            return;
        }

        var i = (int) args.Index;
        _builder.Clear();
        _builder.Append(sequence.Bases);
        _builder[i] = data.Bases[i];
        sequence.Bases = _builder.ToString();
        _builder.Clear();
        _builder.Append(sequence.OriginalBases);
        _builder[i] = data.Bases[i];
        sequence.OriginalBases = _builder.ToString();

        ent.Comp.NextUse = _timing.CurTime + ent.Comp.Cooldown;
        Dirty(ent);

        _adminLog.Add(LogType.Genetics, LogImpact.Low,
            $"{args.Actor:user} used the joker on base {i} of {mob:target} sequence {args.Sequence} with console {ent.Owner:console}");
        UpdateUI(ent.Owner);
    }
}
