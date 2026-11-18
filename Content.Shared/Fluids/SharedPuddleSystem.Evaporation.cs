// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Shared.Chemistry.Components;
using Robust.Shared.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Fluids.Components;
using Content.Shared.Chemistry.Reagent;

namespace Content.Shared.Fluids;

public abstract partial class SharedPuddleSystem
{
    private static readonly TimeSpan EvaporationCooldown = TimeSpan.FromSeconds(1);

    private void OnEvaporationMapInit(Entity<EvaporationComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextTick = _timing.CurTime + EvaporationCooldown;
        Dirty(ent);
    }

    private void UpdateEvaporation(EntityUid uid, Solution solution)
    {
        // Pirate: dry mixtures should leave the evaporation query immediately.
        foreach (var (reagent, _) in solution.Contents)
        {
            if (_prototypeManager.Index<ReagentPrototype>(reagent.Prototype).EvaporationSpeed <= 0)
                continue;

            if (!_evaporationQuery.TryComp(uid, out var evaporation))
            {
                evaporation = AddComp<EvaporationComponent>(uid);
                evaporation.NextTick = _timing.CurTime + EvaporationCooldown;
                Dirty(uid, evaporation);
            }
            else
            {
                foreach (var id in evaporation.Remainders.Keys)
                {
                    if (!solution.ContainsPrototype(id) && evaporation.Remainders.Remove(id))
                        Dirty(uid, evaporation);
                }
            }
            return;
        }

        RemComp<EvaporationComponent>(uid);
    }

    private void TickEvaporation()
    {
        var query = EntityQueryEnumerator<EvaporationComponent, PuddleComponent>();
        var curTime = _timing.CurTime;
        while (query.MoveNext(out var uid, out var evaporation, out var puddle))
        {
            if (evaporation.NextTick > curTime)
                continue;

            // Necessary to keep client and server in sync so they don't drift
            evaporation.NextTick += EvaporationCooldown;
            Dirty(uid, evaporation);

            if (!_solutionContainerSystem.ResolveSolution(uid, puddle.SolutionName, ref puddle.Solution, out var puddleSolution))
                continue;

            // If we have multiple evaporating reagents in one puddle, just take the average evaporation speed and apply
            // that to all of them.
            var evaporationSpeeds = GetEvaporationSpeeds(puddleSolution);
            if (evaporationSpeeds.Count == 0)
            {
                RemComp<EvaporationComponent>(uid); // Pirate: also recover stale saved state.
                continue;
            }

            // Can't use .Average because FixedPoint2
            var evaporationSpeed = evaporationSpeeds.Values.Sum() / evaporationSpeeds.Count;
            // Pirate: keep the original proportions for this entire tick, including traces.
            var originalVolume = puddleSolution.Volume.Double();
            var amount = evaporation.EvaporationAmount.Double() * EvaporationCooldown.TotalSeconds * evaporationSpeed.Double();
            var changed = false;

            // Still have to iterate over one-by-one since the full solution could have non-evaporating solutions.
            foreach (var reagent in evaporationSpeeds.Keys)
            {
                var quantity = puddleSolution.GetTotalPrototypeQuantity(reagent);
                var due = evaporation.Remainders.GetValueOrDefault(reagent) + amount * quantity.Double() / originalVolume;
                var removable = FixedPoint2.Min(quantity, FixedPoint2.New(due));
                var remainder = due - removable.Double();
                if (removable == quantity || remainder <= 0)
                    evaporation.Remainders.Remove(reagent);
                else
                    evaporation.Remainders[reagent] = remainder;

                if (removable <= 0)
                    continue;

                puddleSolution.SplitSolutionWithOnly(removable, reagent);
                changed = true;
            }

            // Despawn if we're done
            if (puddleSolution.Volume == FixedPoint2.Zero)
            {
                // Spawn a *sparkle*
                SpawnAttachedTo(evaporation.EvaporationEffect, Transform(uid).Coordinates);
                PredictedQueueDel(uid);
            }

            if (changed) // Pirate: no reactions or solution replication for a fractional tick.
                _solutionContainerSystem.UpdateChemicals(puddle.Solution.Value);
        }
    }


    public ProtoId<ReagentPrototype>[] GetEvaporatingReagents(Solution solution)
    {
        List<ProtoId<ReagentPrototype>> evaporatingReagents = [];
        foreach (var solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.EvaporationSpeed > FixedPoint2.Zero)
                evaporatingReagents.Add(solProto.ID);
        }
        return evaporatingReagents.ToArray();
    }

    public ProtoId<ReagentPrototype>[] GetAbsorbentReagents(Solution solution)
    {
        var absorbentReagents = new List<ProtoId<ReagentPrototype>>();
        foreach (ReagentPrototype solProto in solution.GetReagentPrototypes(_prototypeManager).Keys)
        {
            if (solProto.Absorbent)
                absorbentReagents.Add(solProto.ID);
        }
        return absorbentReagents.ToArray();
    }

    public bool CanFullyEvaporate(Solution solution)
    {
        return solution.GetTotalPrototypeQuantity(GetEvaporatingReagents(solution)) == solution.Volume;
    }

    /// <summary>
    /// Gets a mapping of evaporating speed of the reagents within a solution.
    /// The speed at which a solution evaporates is the average of the speed of all evaporating reagents in it.
    /// </summary>
    public Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> GetEvaporationSpeeds(Solution solution)
    {
        Dictionary<ProtoId<ReagentPrototype>, FixedPoint2> evaporatingSpeeds = [];
        foreach (var (reagent, _) in solution.Contents) // Pirate: no intermediate quantity dictionary.
        {
            var proto = _prototypeManager.Index<ReagentPrototype>(reagent.Prototype);
            if (proto.EvaporationSpeed > 0)
                evaporatingSpeeds.TryAdd(proto.ID, proto.EvaporationSpeed);
        }
        return evaporatingSpeeds;
    }
}
