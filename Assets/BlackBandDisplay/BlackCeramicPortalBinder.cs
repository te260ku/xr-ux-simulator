using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Renderer))]
public sealed class BlackCeramicPortalBinder :
    MonoBehaviour
{
    public enum InteriorFace
    {
        BackFace = 0,
        FrontFace = 1
    }

    [Header("Target")]

    [SerializeField]
    private Renderer targetRenderer;

    [SerializeField]
    private RenderTexture guiRenderTexture;

    [Header("Virtual GUI plane")]

    [Tooltip(
        "描画されない仮想GUI平面のTransform")]
    [SerializeField]
    private Transform virtualGuiPlane;

    [Tooltip(
        "仮想GUI平面の幅と高さ[m]")]
    [SerializeField]
    private Vector2 planeSize =
        new Vector2(0.8f, 0.12f);

    [Header("Appearance")]

    [SerializeField]
    private Color blackCeramicColor =
        new Color(
            0.005f,
            0.005f,
            0.005f,
            1f);

    [SerializeField]
    private Color guiTint =
        Color.white;

    [Min(0f)]
    [SerializeField]
    private float guiIntensity = 1f;

    [SerializeField]
    private bool flipX;

    [SerializeField]
    private bool flipY;

    [Tooltip(
        "車内から見える側が三角形の表か裏かを指定")]
    [SerializeField]
    private InteriorFace interiorFace =
        InteriorFace.BackFace;

    [Min(0f)]
    [Tooltip(
        "仮想平面が黒セラより確実に奥にあると"
        + "判定するための余裕[m]")]
    [SerializeField]
    private float portalDepthEpsilon =
        0.0005f;

    private static readonly int GuiTexId =
        Shader.PropertyToID("_GuiTex");

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int GuiTintId =
        Shader.PropertyToID("_GuiTint");

    private static readonly int GuiIntensityId =
        Shader.PropertyToID("_GuiIntensity");

    private static readonly int FlipXId =
        Shader.PropertyToID("_FlipX");

    private static readonly int FlipYId =
        Shader.PropertyToID("_FlipY");

    private static readonly int InteriorIsFrontFaceId =
        Shader.PropertyToID(
            "_InteriorIsFrontFace");

    private static readonly int PortalEpsilonId =
        Shader.PropertyToID(
            "_PortalEpsilon");

    private static readonly int PlaneOriginWsId =
        Shader.PropertyToID(
            "_PlaneOriginWS");

    private static readonly int PlaneRightWsId =
        Shader.PropertyToID(
            "_PlaneRightWS");

    private static readonly int PlaneUpWsId =
        Shader.PropertyToID(
            "_PlaneUpWS");

    private static readonly int PlaneNormalWsId =
        Shader.PropertyToID(
            "_PlaneNormalWS");

    private static readonly int PlaneSizeId =
        Shader.PropertyToID(
            "_PlaneSize");

    private MaterialPropertyBlock propertyBlock;

    private void Reset()
    {
        targetRenderer =
            GetComponent<Renderer>();
    }

    private void OnEnable()
    {
        ApplyProperties();
    }

    private void LateUpdate()
    {
        ApplyProperties();
    }

    private void OnValidate()
    {
        planeSize.x =
            Mathf.Max(
                planeSize.x,
                0.0001f);

        planeSize.y =
            Mathf.Max(
                planeSize.y,
                0.0001f);

        portalDepthEpsilon =
            Mathf.Max(
                portalDepthEpsilon,
                0f);

        ApplyProperties();
    }

    private void ApplyProperties()
    {
        if (targetRenderer == null ||
            guiRenderTexture == null ||
            virtualGuiPlane == null)
        {
            return;
        }

        propertyBlock ??=
            new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(
            propertyBlock);

        propertyBlock.SetTexture(
            GuiTexId,
            guiRenderTexture);

        propertyBlock.SetColor(
            BaseColorId,
            blackCeramicColor);

        propertyBlock.SetColor(
            GuiTintId,
            guiTint);

        propertyBlock.SetFloat(
            GuiIntensityId,
            guiIntensity);

        propertyBlock.SetFloat(
            FlipXId,
            flipX ? 1f : 0f);

        propertyBlock.SetFloat(
            FlipYId,
            flipY ? 1f : 0f);

        propertyBlock.SetFloat(
            InteriorIsFrontFaceId,
            interiorFace ==
            InteriorFace.FrontFace
                ? 1f
                : 0f);

        propertyBlock.SetFloat(
            PortalEpsilonId,
            portalDepthEpsilon);

        SetDirection(
            propertyBlock,
            PlaneOriginWsId,
            virtualGuiPlane.position,
            1f);

        SetDirection(
            propertyBlock,
            PlaneRightWsId,
            virtualGuiPlane.right.normalized,
            0f);

        SetDirection(
            propertyBlock,
            PlaneUpWsId,
            virtualGuiPlane.up.normalized,
            0f);

        SetDirection(
            propertyBlock,
            PlaneNormalWsId,
            virtualGuiPlane.forward.normalized,
            0f);

        propertyBlock.SetVector(
            PlaneSizeId,
            new Vector4(
                planeSize.x,
                planeSize.y,
                0f,
                0f));

        targetRenderer.SetPropertyBlock(
            propertyBlock);
    }

    private static void SetDirection(
        MaterialPropertyBlock block,
        int propertyId,
        Vector3 value,
        float w)
    {
        block.SetVector(
            propertyId,
            new Vector4(
                value.x,
                value.y,
                value.z,
                w));
    }

    private void OnDrawGizmosSelected()
    {
        if (virtualGuiPlane == null)
        {
            return;
        }

        Vector3 center =
            virtualGuiPlane.position;

        Vector3 right =
            virtualGuiPlane.right *
            planeSize.x *
            0.5f;

        Vector3 up =
            virtualGuiPlane.up *
            planeSize.y *
            0.5f;

        Gizmos.DrawLine(
            center - right - up,
            center + right - up);

        Gizmos.DrawLine(
            center + right - up,
            center + right + up);

        Gizmos.DrawLine(
            center + right + up,
            center - right + up);

        Gizmos.DrawLine(
            center - right + up,
            center - right - up);

        Gizmos.DrawRay(
            center,
            virtualGuiPlane.forward *
            0.1f);
    }
}