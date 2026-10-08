// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Pirate.Knowledge;

public sealed class PilotingSkillSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public static readonly EntProtoId PilotingKnowledge = "PilotingKnowledge";

    public const int UnskilledPilotFallback = 50;

    public const int SteadyLevel = 25;

    // Prevents training by spinning in place or pushing against a dock.
    public const float MinTrainingSpeed = 1f;

    public const int FlightExperience = 1;

    private const float DriftStrength = 0.8f;
    private const float OvershootStrength = 0.75f;
    private const float DropChance = 0.3f;
    private const float InvertChance = 0.12f;
    private const float SurgeStrength = 0.6f;

    private const int InvertXConfusion = 1;
    private const int InvertYConfusion = 1 << 1;
    private const int InvertRotationConfusion = 1 << 2;
    private const int DiagonalConfusion = 1 << 3;
    private static readonly TimeSpan RollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan DropDuration = TimeSpan.FromSeconds(0.5);
    private const float MinConfusionSeconds = 3f;
    private const float MaxConfusionSeconds = 6f;

    public static float HandlingMultiplier(int level)
    {
        level = Math.Clamp(level, 0, 100);
        return level switch
        {
            < 25 => 0.3f + 0.3f * level / 25f,
            < 50 => 0.6f + 0.4f * (level - 25) / 25f,
            _ => 1f + 0.3f * (level - 50) / 50f,
        };
    }

    public static float Erratic(int level)
        => Math.Clamp(SteadyLevel - level, 0, SteadyLevel) / (float) SteadyLevel;

    public int GetPilotingLevel(EntityUid pilot)
    {
        if (_knowledge.GetContainer(pilot) is not { } store)
            return UnskilledPilotFallback;

        return _knowledge.GetKnowledge(store, PilotingKnowledge)?.Comp.NetLevel ?? 0;
    }

    public (Vector2 Strafe, float Rotation, float Brakes) AdjustInput(
        EntityUid pilot,
        Vector2 strafe,
        float rotation,
        float brakes,
        Vector2 shuttleVelocity)
    {
        var steering = strafe != Vector2.Zero || rotation != 0f || brakes > 0f;
        if (!_knowledge.SkillsEnabled || !steering)
            return (strafe, rotation, brakes);

        if (shuttleVelocity.Length() > MinTrainingSpeed && _knowledge.GetContainer(pilot) is { } store)
            _knowledge.AddExperience(store, PilotingKnowledge, FlightExperience);

        var level = GetPilotingLevel(pilot);
        var erratic = Erratic(level);
        if (erratic > 0f && !ApplyErratic(pilot, erratic, ref strafe, ref rotation))
            return (Vector2.Zero, 0f, 0f);

        var handling = HandlingMultiplier(level);
        return (strafe * handling, rotation * handling, brakes * handling);
    }

    public static Vector2 SwapDiagonal(Vector2 strafe, float sign)
    {
        const float c = 0.70710677f;
        var s = c * sign;
        return new Vector2(strafe.X * c - strafe.Y * s, strafe.X * s + strafe.Y * c);
    }

    private bool ApplyErratic(EntityUid pilot, float erratic, ref Vector2 strafe, ref float rotation)
    {
        var state = EnsureComp<PilotingErraticComponent>(pilot);
        var now = _timing.CurTime;

        if (now >= state.NextRoll)
        {
            state.NextRoll = now + RollInterval;

            if (_random.Prob(DropChance * erratic))
                state.DroppedUntil = now + DropDuration;

            // New confusions can coexist with active ones.
            if (_random.Prob(InvertChance * erratic))
            {
                var confusion = 1 << _random.Next(4);
                if ((state.ConfusedControls & confusion) == 0)
                {
                    if (confusion == DiagonalConfusion)
                        state.DiagonalSign = _random.Prob(0.5f) ? 1f : -1f;

                    state.ConfusedControls |= confusion;
                    var until = now + TimeSpan.FromSeconds(_random.NextFloat(MinConfusionSeconds, MaxConfusionSeconds));
                    if (until > state.ConfusedUntil)
                        state.ConfusedUntil = until;

                    _popup.PopupEntity(Loc.GetString("piloting-confused-controls"), pilot, pilot, PopupType.SmallCaution);
                }
            }
        }

        if (now < state.DroppedUntil)
            return false;

        if (state.ConfusedControls != 0 && now >= state.ConfusedUntil)
            state.ConfusedControls = 0;

        if ((state.ConfusedControls & InvertXConfusion) != 0)
            strafe.X = -strafe.X;
        if ((state.ConfusedControls & InvertYConfusion) != 0)
            strafe.Y = -strafe.Y;
        if ((state.ConfusedControls & InvertRotationConfusion) != 0)
            rotation = -rotation;
        if ((state.ConfusedControls & DiagonalConfusion) != 0)
            strafe = SwapDiagonal(strafe, state.DiagonalSign);

        if (strafe != Vector2.Zero)
        {
            if (now >= state.NextDriftChange)
            {
                state.DriftSign = _random.Prob(0.5f) ? 1f : -1f;
                state.NextDriftChange = now + TimeSpan.FromSeconds(_random.NextFloat(2f, 4f));
            }

            var sideways = new Vector2(-strafe.Y, strafe.X) * (state.DriftSign * DriftStrength * erratic);
            strafe += sideways;
        }

        if (now >= state.NextSurgeChange)
        {
            state.SurgeFactor = 1f + SurgeStrength * erratic * _random.NextFloat(-1f, 1f);
            state.NextSurgeChange = now + TimeSpan.FromSeconds(_random.NextFloat(1f, 2f));
        }

        strafe *= state.SurgeFactor;

        rotation *= 1f + OvershootStrength * erratic;
        return true;
    }
}
