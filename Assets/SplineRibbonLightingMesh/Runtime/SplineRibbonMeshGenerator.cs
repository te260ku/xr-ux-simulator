using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRibbonLighting
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SplineRibbonMeshGenerator : MonoBehaviour
    {
        [Serializable]
        public sealed class DivisionPointSetting
        {
            [Tooltip("Offset in meters from the equally-spaced base position. Positive moves toward the next point number.")]
            public float splineAOffsetMeters;
            [Tooltip("Offset in meters from the equally-spaced base position. Positive moves toward the next point number.")]
            public float splineBOffsetMeters;
        }

        public enum SplineSide { A, B }

        [Header("Splines")]
        [SerializeField] private SplineContainer splineA;
        [SerializeField] private SplineContainer splineB;
        [Tooltip("Enable when Spline B was authored in the opposite direction to Spline A.")]
        [SerializeField] private bool reverseSplineB;

        [Header("Boundary Points")]
        [Min(2)] [SerializeField] private int pointCount = 6;
        [SerializeField] private bool allowEndpointAdjustment;
        [Min(0.0001f)] [SerializeField] private float minimumPointGapMeters = 0.005f;
        [SerializeField] private List<DivisionPointSetting> pointSettings = new List<DivisionPointSetting>();

        [Header("Mesh")]
        [Min(1)] [SerializeField] private int lengthSegmentsPerUnit = 8;
        [Min(32)] [SerializeField] private int splineArcLengthResolution = 512;
        [SerializeField] private Material lightingMaterial;
        [SerializeField] private bool flipNormals;
        [SerializeField] private string generatedRootName = "__GeneratedLightingSurfaces";

        [Header("Scene Handles")]
        [SerializeField] private bool showSceneHandles = true;
        [Range(0.02f, 0.3f)] [SerializeField] private float handleScreenSize = 0.08f;
        [Min(2)] [SerializeField] private int handleProjectionWindowSamples = 32;

        [Header("Runtime")]
        [SerializeField] private bool rebuildOnAwake = true;

        [NonSerialized] private SplineArcLengthTable arcA;
        [NonSerialized] private SplineArcLengthTable arcB;

        public int PointCount => pointCount;
        public bool ShowSceneHandles => showSceneHandles;
        public float HandleScreenSize => handleScreenSize;
        public SplineContainer SplineA => splineA;
        public SplineContainer SplineB => splineB;

        private void Awake()
        {
            if (Application.isPlaying && rebuildOnAwake) Rebuild();
        }

        private void OnValidate()
        {
            pointCount = Mathf.Max(2, pointCount);
            lengthSegmentsPerUnit = Mathf.Max(1, lengthSegmentsPerUnit);
            splineArcLengthResolution = Mathf.Max(32, splineArcLengthResolution);
            handleProjectionWindowSamples = Mathf.Max(2, handleProjectionWindowSamples);
            minimumPointGapMeters = Mathf.Max(0.0001f, minimumPointGapMeters);
            EnsurePointSettings();
        }

        [ContextMenu("Rebuild Lighting Meshes")]
        public void Rebuild()
        {
            ValidateInputs();
            EnsurePointSettings();
            RebuildArcLengthTables();
            ClampAllPointOffsets();

            Transform root = GetOrCreateGeneratedRoot();
            ClearGeneratedChildren(root);

            for (int unitIndex = 0; unitIndex < pointCount - 1; unitIndex++)
            {
                Mesh mesh = BuildUnitMesh(unitIndex);
                GameObject go = new GameObject($"LightingUnit_{unitIndex:00}");
                go.transform.SetParent(root, false);
                MeshFilter filter = go.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                if (lightingMaterial != null) renderer.sharedMaterial = lightingMaterial;
                LightingSurfaceUnit unit = go.AddComponent<LightingSurfaceUnit>();
                unit.Initialize(unitIndex, renderer);
            }
        }

        [ContextMenu("Clear Generated Lighting Meshes")]
        public void ClearGenerated()
        {
            Transform root = FindGeneratedRoot();
            if (root != null) DestroyUnityObject(root.gameObject);
        }

        public void EnsurePointSettings()
        {
            if (pointSettings == null) pointSettings = new List<DivisionPointSetting>();
            while (pointSettings.Count < pointCount) pointSettings.Add(new DivisionPointSetting());
            while (pointSettings.Count > pointCount) pointSettings.RemoveAt(pointSettings.Count - 1);
        }

        public void ResetPointOffsets()
        {
            EnsurePointSettings();
            foreach (DivisionPointSetting p in pointSettings)
            {
                p.splineAOffsetMeters = 0f;
                p.splineBOffsetMeters = 0f;
            }
        }

        public void RebuildArcLengthTables()
        {
            if (splineA == null || splineB == null) return;
            arcA = new SplineArcLengthTable(splineA, splineArcLengthResolution);
            arcB = new SplineArcLengthTable(splineB, splineArcLengthResolution);
        }

        public Vector3 GetPointWorld(SplineSide side, int pointIndex)
        {
            EnsureArcLengthTables();
            float d = GetPointLogicalDistance(side, pointIndex);
            return side == SplineSide.A ? arcA.EvaluateWorld(d, false) : arcB.EvaluateWorld(d, reverseSplineB);
        }

        public Vector3 GetPointTangentWorld(SplineSide side, int pointIndex)
        {
            EnsureArcLengthTables();
            float d = GetPointLogicalDistance(side, pointIndex);
            return side == SplineSide.A ? arcA.EvaluateLogicalTangentWorld(d, false) : arcB.EvaluateLogicalTangentWorld(d, reverseSplineB);
        }

        public float ProjectHandleWorldPositionToLogicalDistance(SplineSide side, int pointIndex, Vector3 draggedWorldPosition)
        {
            EnsureArcLengthTables();
            float current = GetPointLogicalDistance(side, pointIndex);
            float projected = side == SplineSide.A
                ? arcA.ProjectWorldPointToLogicalDistance(draggedWorldPosition, current, false, handleProjectionWindowSamples)
                : arcB.ProjectWorldPointToLogicalDistance(draggedWorldPosition, current, reverseSplineB, handleProjectionWindowSamples);
            return ClampPointLogicalDistance(side, pointIndex, projected);
        }

        public void SetPointLogicalDistance(SplineSide side, int pointIndex, float requestedLogicalDistance)
        {
            ValidatePointIndex(pointIndex);
            EnsureArcLengthTables();
            EnsurePointSettings();
            float clamped = ClampPointLogicalDistance(side, pointIndex, requestedLogicalDistance);
            float offset = clamped - GetBaseLogicalDistance(side, pointIndex);
            if (side == SplineSide.A) pointSettings[pointIndex].splineAOffsetMeters = offset;
            else pointSettings[pointIndex].splineBOffsetMeters = offset;
        }

        public float GetPointLogicalDistance(SplineSide side, int pointIndex)
        {
            ValidatePointIndex(pointIndex);
            EnsureArcLengthTables();
            EnsurePointSettings();
            float offset = side == SplineSide.A ? pointSettings[pointIndex].splineAOffsetMeters : pointSettings[pointIndex].splineBOffsetMeters;
            return Mathf.Clamp(GetBaseLogicalDistance(side, pointIndex) + offset, 0f, GetSplineLength(side));
        }

        public bool IsPointDraggable(int pointIndex)
        {
            ValidatePointIndex(pointIndex);
            return allowEndpointAdjustment || (pointIndex > 0 && pointIndex < pointCount - 1);
        }

        private Mesh BuildUnitMesh(int unitIndex)
        {
            float a0 = GetPointLogicalDistance(SplineSide.A, unitIndex);
            float a1 = GetPointLogicalDistance(SplineSide.A, unitIndex + 1);
            float b0 = GetPointLogicalDistance(SplineSide.B, unitIndex);
            float b1 = GetPointLogicalDistance(SplineSide.B, unitIndex + 1);

            int rows = lengthSegmentsPerUnit + 1;
            var vertices = new List<Vector3>(rows * 2);
            var uv0 = new List<Vector2>(rows * 2);
            var uv1 = new List<Vector2>(rows * 2);
            var triangles = new List<int>(lengthSegmentsPerUnit * 6);
            float lenA = Mathf.Max(arcA.Length, 1e-6f);
            float lenB = Mathf.Max(arcB.Length, 1e-6f);

            for (int j = 0; j <= lengthSegmentsPerUnit; j++)
            {
                float s = j / (float)lengthSegmentsPerUnit;
                float da = Mathf.Lerp(a0, a1, s);
                float db = Mathf.Lerp(b0, b1, s);
                Vector3 worldA = arcA.EvaluateWorld(da, false);
                Vector3 worldB = arcB.EvaluateWorld(db, reverseSplineB);
                vertices.Add(transform.InverseTransformPoint(worldA));
                vertices.Add(transform.InverseTransformPoint(worldB));

                // UV0: per lighting unit. A(back)=1, B(front)=0.
                uv0.Add(new Vector2(s, 1f));
                uv0.Add(new Vector2(s, 0f));

                // UV1: continuous position over the whole ribbon.
                float globalU = 0.5f * (da / lenA + db / lenB);
                uv1.Add(new Vector2(globalU, 1f));
                uv1.Add(new Vector2(globalU, 0f));
            }

            for (int j = 0; j < lengthSegmentsPerUnit; j++)
            {
                int va0 = j * 2;
                int vb0 = va0 + 1;
                int va1 = (j + 1) * 2;
                int vb1 = va1 + 1;
                if (!flipNormals)
                {
                    triangles.Add(va0); triangles.Add(va1); triangles.Add(vb0);
                    triangles.Add(va1); triangles.Add(vb1); triangles.Add(vb0);
                }
                else
                {
                    triangles.Add(va0); triangles.Add(vb0); triangles.Add(va1);
                    triangles.Add(va1); triangles.Add(vb0); triangles.Add(vb1);
                }
            }

            Mesh mesh = new Mesh { name = $"LightingUnitMesh_{unitIndex:00}" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private float ClampPointLogicalDistance(SplineSide side, int pointIndex, float requested)
        {
            ValidatePointIndex(pointIndex);
            float length = GetSplineLength(side);
            if (!allowEndpointAdjustment)
            {
                if (pointIndex == 0) return 0f;
                if (pointIndex == pointCount - 1) return length;
            }

            float min = 0f;
            float max = length;
            if (pointIndex > 0) min = GetPointLogicalDistance(side, pointIndex - 1) + minimumPointGapMeters;
            if (pointIndex < pointCount - 1) max = GetPointLogicalDistance(side, pointIndex + 1) - minimumPointGapMeters;
            if (min > max) { float mid = (min + max) * 0.5f; min = mid; max = mid; }
            return Mathf.Clamp(requested, min, max);
        }

        private void ClampAllPointOffsets()
        {
            EnsurePointSettings();
            if (!allowEndpointAdjustment)
            {
                pointSettings[0].splineAOffsetMeters = 0f;
                pointSettings[0].splineBOffsetMeters = 0f;
                pointSettings[pointCount - 1].splineAOffsetMeters = 0f;
                pointSettings[pointCount - 1].splineBOffsetMeters = 0f;
            }

            // Forward order guarantees each point sees an already-valid previous point.
            ClampSideForward(SplineSide.A);
            ClampSideForward(SplineSide.B);
        }

        private void ClampSideForward(SplineSide side)
        {
            for (int i = 0; i < pointCount; i++)
            {
                float current = GetPointLogicalDistance(side, i);
                SetPointLogicalDistance(side, i, current);
            }
        }

        private float GetBaseLogicalDistance(SplineSide side, int pointIndex)
        {
            return GetSplineLength(side) * pointIndex / (float)(pointCount - 1);
        }

        private float GetSplineLength(SplineSide side)
        {
            EnsureArcLengthTables();
            return side == SplineSide.A ? arcA.Length : arcB.Length;
        }

        private void EnsureArcLengthTables()
        {
            if (arcA == null || arcB == null) RebuildArcLengthTables();
            if (arcA == null || arcB == null) throw new InvalidOperationException("Both Spline A and Spline B must be assigned.");
        }

        private void ValidateInputs()
        {
            if (splineA == null) throw new InvalidOperationException("Spline A is not assigned.");
            if (splineB == null) throw new InvalidOperationException("Spline B is not assigned.");
            if (pointCount < 2) throw new InvalidOperationException("Point Count must be at least 2.");
            RebuildArcLengthTables();
            if (arcA.Length <= 1e-6f || arcB.Length <= 1e-6f) throw new InvalidOperationException("Spline length must be greater than zero.");
        }

        private void ValidatePointIndex(int pointIndex)
        {
            if (pointIndex < 0 || pointIndex >= pointCount) throw new ArgumentOutOfRangeException(nameof(pointIndex));
        }

        private Transform GetOrCreateGeneratedRoot()
        {
            Transform root = FindGeneratedRoot();
            if (root != null) return root;
            GameObject go = new GameObject(string.IsNullOrWhiteSpace(generatedRootName) ? "__GeneratedLightingSurfaces" : generatedRootName);
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        private Transform FindGeneratedRoot()
        {
            string rootName = string.IsNullOrWhiteSpace(generatedRootName) ? "__GeneratedLightingSurfaces" : generatedRootName;
            return transform.Find(rootName);
        }

        private static void ClearGeneratedChildren(Transform root)
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                MeshFilter filter = child.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null) DestroyUnityObject(filter.sharedMesh);
                DestroyUnityObject(child.gameObject);
            }
        }

        private static void DestroyUnityObject(UnityEngine.Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying) { UnityEngine.Object.DestroyImmediate(obj); return; }
#endif
            UnityEngine.Object.Destroy(obj);
        }
    }
}
