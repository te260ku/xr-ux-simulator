using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public sealed class LEDOverlayController : MonoBehaviour
{
    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");


    [Header("Default State")]

    [SerializeField]
    private Color _color = Color.white;

    [SerializeField]
    [Min(0f)]
    private float _intensity = 0f;


    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;


    private void OnEnable()
    {
        Initialize();
        RestoreVisualState();
    }


    private void OnValidate()
    {
        Initialize();
        RestoreVisualState();
    }


    /// <summary>
    /// Timelineなど外部から一時的な表示状態を設定する。
    /// SerializedField自体は変更しない。
    /// </summary>
    public void SetVisualState(
        Color color,
        float intensity)
    {
        Initialize();

        _renderer.GetPropertyBlock(
            _propertyBlock);

        _propertyBlock.SetColor(
            ColorId,
            color);

        _propertyBlock.SetFloat(
            IntensityId,
            Mathf.Max(0f, intensity));

        _renderer.SetPropertyBlock(
            _propertyBlock);
    }


    /// <summary>
    /// Inspectorで設定されているデフォルト状態へ戻す。
    /// </summary>
    public void RestoreVisualState()
    {
        SetVisualState(
            _color,
            _intensity);
    }


    private void Initialize()
    {
        if (_renderer == null)
        {
            _renderer =
                GetComponent<Renderer>();
        }

        _propertyBlock ??=
            new MaterialPropertyBlock();
    }
}