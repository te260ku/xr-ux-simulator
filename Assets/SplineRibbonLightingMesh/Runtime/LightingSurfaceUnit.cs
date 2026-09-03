using UnityEngine;

namespace SplineRibbonLighting
{
    [DisallowMultipleComponent]
    public sealed class LightingSurfaceUnit : MonoBehaviour
    {
        private static readonly int LightColorId = Shader.PropertyToID("_LightColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int FalloffPowerId = Shader.PropertyToID("_FalloffPower");

        [SerializeField] private int unitIndex;
        [SerializeField] private MeshRenderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;

        public int UnitIndex => unitIndex;

        internal void Initialize(int index, MeshRenderer renderer)
        {
            unitIndex = index;
            targetRenderer = renderer;
        }

        public void SetLight(Color color, float intensity)
        {
            EnsurePropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(LightColorId, color);
            propertyBlock.SetFloat(IntensityId, Mathf.Max(0f, intensity));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        public void SetFalloffPower(float falloffPower)
        {
            EnsurePropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FalloffPowerId, Mathf.Max(0.001f, falloffPower));
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private void EnsurePropertyBlock()
        {
            if (targetRenderer == null) targetRenderer = GetComponent<MeshRenderer>();
            if (targetRenderer == null) throw new MissingComponentException("LightingSurfaceUnit requires a MeshRenderer.");
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
        }
    }
}
