using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

internal sealed class LedSequencePreviewController
{
    private sealed class Target
    {
        public string Id;
        public LedElement Element;
        public LEDOverlayController Controller;
    }


    private readonly LedSequenceEvaluator _evaluator =
        new();

    private readonly List<Target> _targets =
        new();

    private readonly List<LedTargetInfo> _targetInfos =
    new();


    public bool IsActive { get; private set; }


    public void Apply(
        LedSequence sequence,
        float time)
    {
        if (sequence == null)
        {
            return;
        }

        if (!IsActive)
        {
            Begin();
        }

        RefreshTargetInfos();

        IReadOnlyDictionary<string, LedState> states =
        _evaluator.Evaluate(
            sequence,
            time,
            _targetInfos);

        foreach (Target target in _targets)
        {
            if (!states.TryGetValue(
                    target.Id,
                    out LedState state))
            {
                continue;
            }

            // target.Controller.SetPreviewState(
            //     state.Color,
            //     state.Intensity);
        }

        SceneView.RepaintAll();
    }

    private void RefreshTargetInfos()
    {
        _targetInfos.Clear();

        foreach (Target target in _targets)
        {
            if (target.Element == null)
            {
                continue;
            }

            _targetInfos.Add(
                new LedTargetInfo(
                    target.Id,
                    target.Element.WorldPosition));
        }
    }


    public void Stop()
    {
        if (!IsActive)
        {
            return;
        }

        foreach (Target target in _targets)
        {
            if (target.Controller == null)
            {
                continue;
            }

            // target.Controller.ClearPreviewState();
        }

        _targets.Clear();
        _targetInfos.Clear();

        IsActive = false;

        SceneView.RepaintAll();
    }


    private void Begin()
    {
        _targets.Clear();
        _targetInfos.Clear();

        LedElement[] leds =
            Object.FindObjectsByType<LedElement>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

        foreach (LedElement led in leds)
        {
            if (!led.gameObject.scene.IsValid())
            {
                continue;
            }

            if (string.IsNullOrEmpty(led.Id))
            {
                continue;
            }

            if (!led.TryGetComponent(
                    out LEDOverlayController controller))
            {
                Debug.LogWarning(
                    $"{led.name} に " +
                    $"{nameof(LEDOverlayController)} がありません。",
                    led);

                continue;
            }

            _targets.Add(
                new Target
                {
                    Id = led.Id,
                    Element = led,
                    Controller = controller
                });

            _targetInfos.Add(
                new LedTargetInfo(
                    led.Id,
                    led.WorldPosition));
        }

        IsActive = true;
    }
}