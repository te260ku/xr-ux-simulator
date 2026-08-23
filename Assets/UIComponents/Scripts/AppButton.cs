using System;
using System.Collections;
using R3;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public sealed class AppButton :
    MonoBehaviour,
    IPointerDownHandler,
    IPointerUpHandler,
    IPointerExitHandler
{
    [Header("Contents")]
    [SerializeField]
    private TMP_Text _label;

    [SerializeField]
    private Image _icon;

    [SerializeField]
    private Image _background;

    [Header("Long Press")]
    [SerializeField]
    private bool _longPressEnabled = false;

    [SerializeField]
    [Min(0.1f)]
    private float _longPressThreshold = 0.5f;

    [Header("Interaction")]
    [SerializeField]
    private AppButtonTapEffectType _tapEffectType =
        AppButtonTapEffectType.Scale;

    [SerializeField]
    private RectTransform _interactionTarget;

    [SerializeField]
    [Range(0.5f, 1f)]
    private float _pressedScale = 0.95f;

    [SerializeField]
    [Min(0f)]
    private float _scaleAnimationDuration = 0.08f;

    [Header("Sound")]
    [SerializeField]
    private AppButtonSoundType _soundType =
        AppButtonSoundType.Default;

    private Button _button;

    private readonly Subject<Unit> _clickSubject = new();
    private readonly Subject<Unit> _longPressSubject = new();

    private IDisposable _clickSubscription;

    private IAppButtonSoundPlayer _soundPlayer;

    private Coroutine _longPressCoroutine;
    private Coroutine _scaleCoroutine;

    private bool _isPointerDown;
    private bool _longPressTriggered;
    private bool _suppressNextClick;

    private Vector3 _normalScale;

    public Observable<Unit> OnClick => _clickSubject;
    public Observable<Unit> OnLongPress => _longPressSubject;

    public bool IsInteractable => _button.interactable;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (_interactionTarget == null)
        {
            _interactionTarget = transform as RectTransform;
        }

        if (_background == null)
        {
            _background = _button.targetGraphic as Image;
        }

        if (_interactionTarget != null)
        {
            _normalScale = _interactionTarget.localScale;
        }

        _clickSubscription = _button
            .OnClickAsObservable()
            .Subscribe(_ => HandleClick());
    }

    private void OnDisable()
    {
        CancelLongPress();
        StopScaleAnimation();

        _isPointerDown = false;
        _longPressTriggered = false;
        _suppressNextClick = false;

        RestoreInteractionImmediately();
    }

    private void OnDestroy()
    {
        _clickSubscription?.Dispose();

        _clickSubject.Dispose();
        _longPressSubject.Dispose();
    }

    // --------------------------------------------------
    // Initialization
    // --------------------------------------------------

    public void Initialize(IAppButtonSoundPlayer soundPlayer)
    {
        _soundPlayer = soundPlayer;
    }

    // --------------------------------------------------
    // Contents
    // --------------------------------------------------

    public void SetText(string text)
    {
        if (_label == null)
        {
            Debug.LogError(
                $"{nameof(AppButton)}: Label is not assigned.",
                this);

            return;
        }

        _label.text = text;
    }

    public void SetIcon(Sprite sprite)
    {
        if (_icon == null)
        {
            Debug.LogError(
                $"{nameof(AppButton)}: Icon is not assigned.",
                this);

            return;
        }

        _icon.sprite = sprite;
        _icon.enabled = sprite != null;
    }

    public void SetBackgroundColor(Color color)
    {
        if (_background == null)
        {
            Debug.LogError(
                $"{nameof(AppButton)}: Background is not assigned.",
                this);

            return;
        }

        _background.color = color;
    }

    // --------------------------------------------------
    // State
    // --------------------------------------------------

    public void SetInteractable(bool interactable)
    {
        _button.interactable = interactable;

        if (!interactable)
        {
            CancelInteraction();
        }
    }

    // --------------------------------------------------
    // Visibility
    // --------------------------------------------------

    public void Show()
    {
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    public bool IsVisible => gameObject.activeSelf;

    // --------------------------------------------------
    // Pointer
    // --------------------------------------------------

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!IsInteractable)
        {
            return;
        }

        _isPointerDown = true;
        _longPressTriggered = false;

        PlayPressedInteraction();

        if (_longPressEnabled)
        {
            StartLongPressDetection();
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_isPointerDown)
        {
            return;
        }

        _isPointerDown = false;

        CancelLongPress();
        PlayReleasedInteraction();

        if (_longPressTriggered)
        {
            _suppressNextClick = true;

            StartCoroutine(
                ClearClickSuppressionAtEndOfFrame());
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!_isPointerDown)
        {
            return;
        }

        CancelLongPress();
        PlayReleasedInteraction();
    }

    // --------------------------------------------------
    // Click
    // --------------------------------------------------

    private void HandleClick()
    {
        if (_suppressNextClick)
        {
            _suppressNextClick = false;
            return;
        }

        if (!IsInteractable)
        {
            return;
        }

        PlaySound();

        _clickSubject.OnNext(Unit.Default);
    }

    // --------------------------------------------------
    // Long Press
    // --------------------------------------------------

    private void StartLongPressDetection()
    {
        CancelLongPress();

        _longPressCoroutine =
            StartCoroutine(LongPressRoutine());
    }

    private IEnumerator LongPressRoutine()
    {
        float elapsed = 0f;

        while (_isPointerDown &&
               elapsed < _longPressThreshold)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        _longPressCoroutine = null;

        if (!_isPointerDown)
        {
            yield break;
        }

        _longPressTriggered = true;

        PlaySound();

        _longPressSubject.OnNext(Unit.Default);
    }

    private void CancelLongPress()
    {
        if (_longPressCoroutine == null)
        {
            return;
        }

        StopCoroutine(_longPressCoroutine);
        _longPressCoroutine = null;
    }

    private IEnumerator ClearClickSuppressionAtEndOfFrame()
    {
        yield return null;

        _suppressNextClick = false;
    }

    // --------------------------------------------------
    // Interaction
    // --------------------------------------------------

    private void PlayPressedInteraction()
    {
        switch (_tapEffectType)
        {
            case AppButtonTapEffectType.None:
                break;

            case AppButtonTapEffectType.Scale:
                AnimateScale(
                    _normalScale * _pressedScale);
                break;
        }
    }

    private void PlayReleasedInteraction()
    {
        switch (_tapEffectType)
        {
            case AppButtonTapEffectType.None:
                break;

            case AppButtonTapEffectType.Scale:
                AnimateScale(_normalScale);
                break;
        }
    }

    private void AnimateScale(Vector3 targetScale)
    {
        if (_interactionTarget == null)
        {
            return;
        }

        StopScaleAnimation();

        if (_scaleAnimationDuration <= 0f)
        {
            _interactionTarget.localScale = targetScale;
            return;
        }

        _scaleCoroutine =
            StartCoroutine(
                ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(
        Vector3 targetScale)
    {
        Vector3 startScale =
            _interactionTarget.localScale;

        float elapsed = 0f;

        while (elapsed < _scaleAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / _scaleAnimationDuration);

            _interactionTarget.localScale =
                Vector3.Lerp(
                    startScale,
                    targetScale,
                    t);

            yield return null;
        }

        _interactionTarget.localScale =
            targetScale;

        _scaleCoroutine = null;
    }

    private void StopScaleAnimation()
    {
        if (_scaleCoroutine == null)
        {
            return;
        }

        StopCoroutine(_scaleCoroutine);
        _scaleCoroutine = null;
    }

    private void RestoreInteractionImmediately()
    {
        if (_interactionTarget == null)
        {
            return;
        }

        _interactionTarget.localScale =
            _normalScale;
    }

    private void CancelInteraction()
    {
        _isPointerDown = false;
        _longPressTriggered = false;
        _suppressNextClick = false;

        CancelLongPress();
        StopScaleAnimation();
        RestoreInteractionImmediately();
    }

    // --------------------------------------------------
    // Sound
    // --------------------------------------------------

    private void PlaySound()
    {
        if (_soundType == AppButtonSoundType.None)
        {
            return;
        }

        _soundPlayer?.Play(_soundType);
    }

#if UNITY_EDITOR

    private void Reset()
    {
        _button = GetComponent<Button>();

        _interactionTarget =
            transform as RectTransform;

        _background =
            GetComponent<Image>();
    }

#endif
}

public enum AppButtonTapEffectType
{
    None,
    Scale
}

public enum AppButtonSoundType
{
    None,
    Default,
    Ok,
    Cancel
}

public interface IAppButtonSoundPlayer
{
    void Play(AppButtonSoundType soundType);
}