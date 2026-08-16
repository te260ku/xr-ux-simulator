using System.Collections.Generic;
using UnityEngine;

public sealed class LedSequenceEvaluator
{
    private readonly Dictionary<string, LedState> _states =
        new();

    private readonly Dictionary<string, float> _activeClipStartTimes =
        new();

    private readonly Dictionary<string, Vector3> _positions =
        new();


    public IReadOnlyDictionary<string, LedState> Evaluate(
        LedSequence sequence,
        float time,
        IReadOnlyList<LedTargetInfo> leds)
    {
        InitializeStates(leds);

        if (sequence == null)
        {
            return _states;
        }

        if (time < 0f)
        {
            return _states;
        }

        float sequenceTime =
            ResolveSequenceTime(
                sequence,
                time);

        if (sequenceTime < 0f)
        {
            return _states;
        }

        foreach (LedEffectClip clip in sequence.Clips)
        {
            EvaluateClip(
                clip,
                sequenceTime);
        }

        return _states;
    }


    // ============================================================
    // Initialize
    // ============================================================

    private void InitializeStates(
        IReadOnlyList<LedTargetInfo> leds)
    {
        _states.Clear();
        _activeClipStartTimes.Clear();
        _positions.Clear();

        foreach (LedTargetInfo led in leds)
        {
            if (string.IsNullOrEmpty(led.Id))
            {
                continue;
            }

            _states[led.Id] =
                LedState.Off;

            _activeClipStartTimes[led.Id] =
                float.NegativeInfinity;

            _positions[led.Id] =
                led.Position;
        }
    }


    private static float ResolveSequenceTime(
        LedSequence sequence,
        float time)
    {
        if (sequence.Loop)
        {
            return Mathf.Repeat(
                time,
                sequence.Duration);
        }

        if (time >= sequence.Duration)
        {
            return -1f;
        }

        return time;
    }


    // ============================================================
    // Clip
    // ============================================================

    private void EvaluateClip(
        LedEffectClip clip,
        float sequenceTime)
    {
        if (clip == null)
        {
            return;
        }

        // Clip全体の時間外なら何もしない
        if (sequenceTime < clip.StartTime ||
            sequenceTime >= clip.EndTime)
        {
            return;
        }

        foreach (string ledId in clip.TargetLedIds)
        {
            if (!_states.ContainsKey(ledId))
            {
                continue;
            }

            float phase =
                EvaluateDistributionPhase(
                    clip,
                    ledId);

            float delay =
                clip.StaggerDuration *
                phase;

            float ledStartTime =
                clip.StartTime +
                delay;

            float localTime =
                sequenceTime -
                ledStartTime;

            if (localTime < 0f ||
                localTime >= clip.Duration)
            {
                continue;
            }

            float normalizedTime =
                Mathf.Clamp01(
                    localTime /
                    clip.Duration);

            float effectIntensity =
                EvaluateEffect(
                    clip.EffectType,
                    normalizedTime);

            float intensity =
                clip.Intensity *
                effectIntensity;

            LedState state =
                new(
                    clip.Color,
                    intensity);

            ApplyState(
                ledId,
                clip.StartTime,
                state);
        }
    }


    // ============================================================
    // Distribution
    // ============================================================

    private float EvaluateDistributionPhase(
        LedEffectClip clip,
        string ledId)
    {
        if (clip.DistributionType ==
            LedDistributionType.Simultaneous)
        {
            return 0f;
        }

        if (!_positions.TryGetValue(
                ledId,
                out Vector3 position))
        {
            return 0f;
        }

        if (!TryGetCoordinateRange(
                clip,
                out float min,
                out float max))
        {
            return 0f;
        }

        float coordinate =
            GetAxisCoordinate(
                position,
                clip.DistributionAxis);

        if (Mathf.Approximately(
                min,
                max))
        {
            return 0f;
        }

        return clip.DistributionType switch
        {
            LedDistributionType.LowToHigh =>
                Mathf.InverseLerp(
                    min,
                    max,
                    coordinate),

            LedDistributionType.HighToLow =>
                1f -
                Mathf.InverseLerp(
                    min,
                    max,
                    coordinate),

            LedDistributionType.CenterOut =>
                EvaluateCenterPhase(
                    clip,
                    coordinate),

            LedDistributionType.OutsideIn =>
                1f -
                EvaluateCenterPhase(
                    clip,
                    coordinate),

            _ =>
                0f
        };
    }


    private bool TryGetCoordinateRange(
        LedEffectClip clip,
        out float min,
        out float max)
    {
        min =
            float.PositiveInfinity;

        max =
            float.NegativeInfinity;

        bool found =
            false;

        foreach (string id in clip.TargetLedIds)
        {
            if (!_positions.TryGetValue(
                    id,
                    out Vector3 position))
            {
                continue;
            }

            float coordinate =
                GetAxisCoordinate(
                    position,
                    clip.DistributionAxis);

            min =
                Mathf.Min(
                    min,
                    coordinate);

            max =
                Mathf.Max(
                    max,
                    coordinate);

            found =
                true;
        }

        return found;
    }


    private float EvaluateCenterPhase(
        LedEffectClip clip,
        float coordinate)
    {
        if (!TryGetCoordinateRange(
                clip,
                out float min,
                out float max))
        {
            return 0f;
        }

        float center =
            (min + max) * 0.5f;

        float currentDistance =
            Mathf.Abs(
                coordinate -
                center);

        float minDistance =
            float.PositiveInfinity;

        float maxDistance =
            float.NegativeInfinity;


        foreach (string id in clip.TargetLedIds)
        {
            if (!_positions.TryGetValue(
                    id,
                    out Vector3 position))
            {
                continue;
            }

            float targetCoordinate =
                GetAxisCoordinate(
                    position,
                    clip.DistributionAxis);

            float distance =
                Mathf.Abs(
                    targetCoordinate -
                    center);

            minDistance =
                Mathf.Min(
                    minDistance,
                    distance);

            maxDistance =
                Mathf.Max(
                    maxDistance,
                    distance);
        }


        if (Mathf.Approximately(
                minDistance,
                maxDistance))
        {
            return 0f;
        }

        return Mathf.InverseLerp(
            minDistance,
            maxDistance,
            currentDistance);
    }


    private static float GetAxisCoordinate(
        Vector3 position,
        LedDistributionAxis axis)
    {
        return axis switch
        {
            LedDistributionAxis.X =>
                position.x,

            LedDistributionAxis.Y =>
                position.y,

            LedDistributionAxis.Z =>
                position.z,

            _ =>
                position.x
        };
    }


    // ============================================================
    // Blend
    // ============================================================

    private void ApplyState(
        string ledId,
        float clipStartTime,
        LedState state)
    {
        if (clipStartTime <
            _activeClipStartTimes[ledId])
        {
            return;
        }

        _states[ledId] =
            state;

        _activeClipStartTimes[ledId] =
            clipStartTime;
    }


    // ============================================================
    // Effect
    // ============================================================

    private static float EvaluateEffect(
        LedEffectType effectType,
        float t)
    {
        return effectType switch
        {
            LedEffectType.Static =>
                1f,

            LedEffectType.Pulse =>
                Mathf.Sin(
                    Mathf.PI *
                    t),

            _ =>
                0f
        };
    }
}