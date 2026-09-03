using UnityEngine;

namespace SplineRibbonLightingNew
{
    [DisallowMultipleComponent]
    public sealed class LightingSurfaceUnit : MonoBehaviour
    {
        private static readonly int LightColorId =
            Shader.PropertyToID("_LightColor");

        private static readonly int IntensityId =
            Shader.PropertyToID("_Intensity");

        private static readonly int FalloffPowerId =
            Shader.PropertyToID("_FalloffPower");

        private static readonly int BackAlphaId =
            Shader.PropertyToID("_BackAlpha");

        private static readonly int FrontAlphaId =
            Shader.PropertyToID("_FrontAlpha");

        [SerializeField] private int unitIndex;
        [SerializeField] private MeshRenderer targetRenderer;

        private MaterialPropertyBlock _block;

        public int UnitIndex => unitIndex;

        internal void Initialize(
            int index,
            MeshRenderer renderer)
        {
            unitIndex = index;
            targetRenderer = renderer;
        }

        public void SetLight(
            Color hdrColor,
            float intensity)
        {
            Ensure();

            targetRenderer.GetPropertyBlock(_block);

            _block.SetColor(
                LightColorId,
                hdrColor);

            _block.SetFloat(
                IntensityId,
                Mathf.Max(0f, intensity));

            targetRenderer.SetPropertyBlock(_block);
        }

        public void SetTransparency(
            float backAlpha,
            float frontAlpha,
            float falloffPower)
        {
            Ensure();

            targetRenderer.GetPropertyBlock(_block);

            _block.SetFloat(
                BackAlphaId,
                Mathf.Clamp01(backAlpha));

            _block.SetFloat(
                FrontAlphaId,
                Mathf.Clamp01(frontAlpha));

            _block.SetFloat(
                FalloffPowerId,
                Mathf.Max(0.001f, falloffPower));

            targetRenderer.SetPropertyBlock(_block);
        }

        private void Ensure()
        {
            if (targetRenderer == null)
                targetRenderer = GetComponent<MeshRenderer>();

            if (targetRenderer == null)
            {
                throw new MissingComponentException(
                    $"{nameof(LightingSurfaceUnit)} requires a MeshRenderer.");
            }

            _block ??= new MaterialPropertyBlock();
        }
    }
}
