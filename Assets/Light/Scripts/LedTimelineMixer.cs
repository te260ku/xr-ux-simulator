using UnityEngine;
using UnityEngine.Playables;

public sealed class LedTimelineMixer
    : PlayableBehaviour
{
    private LEDOverlayController _boundLed;


    public override void ProcessFrame(
        Playable playable,
        FrameData info,
        object playerData)
    {
        _boundLed =
            playerData as LEDOverlayController;

        if (_boundLed == null)
        {
            return;
        }


        int inputCount =
            playable.GetInputCount();

        Color blendedColor =
            Color.black;

        float blendedIntensity =
            0f;

        float totalWeight =
            0f;


        for (int i = 0;
             i < inputCount;
             i++)
        {
            float weight =
                playable.GetInputWeight(i);

            if (weight <= 0f)
            {
                continue;
            }


            Playable inputPlayable =
                playable.GetInput(i);

            var input =
                (ScriptPlayable<LedTimelineBehaviour>)
                inputPlayable;

            LedTimelineBehaviour behaviour =
                input.GetBehaviour();


            // ----------------------------------
            // Clip内の正規化時間 0～1
            // ----------------------------------

            double duration =
                inputPlayable.GetDuration();

            float t;

            if (duration <= 0.0 ||
                double.IsInfinity(duration))
            {
                t = 0f;
            }
            else
            {
                t = Mathf.Clamp01(
                    (float)(
                        inputPlayable.GetTime()
                        /
                        duration));
            }


            // ----------------------------------
            // Color
            // ----------------------------------

            Color color =
                behaviour.Color != null
                    ? behaviour.Color.Evaluate(t)
                    : Color.white;


            // ----------------------------------
            // Intensity
            // ----------------------------------

            float intensity =
                behaviour.Intensity != null
                    ? behaviour.Intensity.Evaluate(t)
                    : 1f;


            // TimelineのBlend / Ease Weightを反映
            blendedColor +=
                color * weight;

            blendedIntensity +=
                intensity * weight;

            totalWeight +=
                weight;
        }


        // ----------------------------------
        // Active Clipなし
        // ----------------------------------

        if (totalWeight <= 0f)
        {
            _boundLed.SetVisualState(
                Color.white,
                0f);

            return;
        }


        // 色はWeightの合計で正規化
        blendedColor /=
            totalWeight;


        _boundLed.SetVisualState(
            blendedColor,
            Mathf.Max(
                0f,
                blendedIntensity));
    }


    public override void OnGraphStop(
        Playable playable)
    {
        if (_boundLed == null)
        {
            return;
        }

        _boundLed.RestoreVisualState();
    }


    public override void OnPlayableDestroy(
        Playable playable)
    {
        if (_boundLed == null)
        {
            return;
        }

        _boundLed.RestoreVisualState();
    }
}