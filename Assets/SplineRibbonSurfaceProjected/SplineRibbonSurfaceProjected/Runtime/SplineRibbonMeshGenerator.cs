using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRibbonLightingNew
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class SplineRibbonMeshGenerator : MonoBehaviour
    {
        [Serializable]
        public sealed class DivisionPointSetting
        {
            [Tooltip(
                "Offset in meters from the equally-spaced base position " +
                "along Spline A.")]
            public float splineAOffsetMeters;

            [Tooltip(
                "Offset in meters from the equally-spaced base position " +
                "along Spline B.")]
            public float splineBOffsetMeters;
        }

        public enum SplineSide
        {
            A,
            B
        }

        [Header("Reference surface")]
        [Tooltip(
            "Existing curved mesh that the splines and generated lighting " +
            "surfaces should follow.")]
        [SerializeField]
        private MeshCollider sourceSurface;

        [Tooltip(
            "Moves generated lighting vertices away from the reference " +
            "surface along its normal to avoid Z-fighting.")]
        [Min(0f)]
        [SerializeField]
        private float surfaceOffsetMeters = 0.001f;

        [Tooltip(
            "Invert the MeshCollider hit normal before applying Surface Offset.")]
        [SerializeField]
        private bool invertSurfaceOffsetNormal;

        [Tooltip(
            "Minimum half-distance used by the projection ray casts. " +
            "The actual cast distance is automatically increased from the " +
            "source collider bounds when necessary.")]
        [Min(0.001f)]
        [SerializeField]
        private float minimumProjectionCastDistance = 0.1f;

        [Header("Spline surface constraint")]
        [Tooltip(
            "In Edit Mode, knot changes are detected and knots are snapped " +
            "back to Source Surface.")]
        [SerializeField]
        private bool constrainSplineKnotsToSurface = true;

        [Tooltip(
            "If enabled, the Editor watcher rebuilds generated meshes when " +
            "Spline A/B knots or transforms change.")]
        [SerializeField]
        private bool autoRebuildWhenSplinesChange = true;

        [Min(0.000001f)]
        [SerializeField]
        private float knotSurfaceTolerance = 0.0001f;

        [Header("Splines")]
        [SerializeField] private SplineContainer splineA;
        [SerializeField] private SplineContainer splineB;

        [Tooltip(
            "Enable if Spline B was authored in the opposite direction.")]
        [SerializeField]
        private bool reverseSplineB;

        [Header("Numbered boundary points")]
        [Min(2)]
        [SerializeField]
        private int pointCount = 6;

        [SerializeField]
        private bool allowEndpointAdjustment;

        [Min(0.0001f)]
        [SerializeField]
        private float minimumPointGapMeters = 0.005f;

        [SerializeField]
        private List<DivisionPointSetting> pointSettings = new();

        [Header("Generated surface resolution")]
        [Tooltip(
            "Segments along each lighting unit.")]
        [Min(1)]
        [SerializeField]
        private int lengthSegmentsPerUnit = 8;

        [Tooltip(
            "Segments from Spline B (front) to Spline A (back). " +
            "Increase this to reproduce source-surface curvature across width.")]
        [Min(1)]
        [SerializeField]
        private int widthSegments = 8;

        [Min(32)]
        [SerializeField]
        private int splineArcLengthResolution = 512;

        [Header("Rendering")]
        [SerializeField]
        private Material lightingMaterial;

        [Tooltip(
            "Reverse generated triangle winding.")]
        [SerializeField]
        private bool flipTriangles;

        [Header("Generated hierarchy")]
        [SerializeField]
        private string generatedRootName =
            "__GeneratedLightingSurfaces";

        [Header("Scene handles")]
        [SerializeField]
        private bool showSceneHandles = true;

        [Range(0.02f, 0.3f)]
        [SerializeField]
        private float handleScreenSize = 0.08f;

        [Min(2)]
        [SerializeField]
        private int handleProjectionWindowSamples = 32;

        [Header("Runtime")]
        [SerializeField]
        private bool rebuildOnAwake = true;

        [NonSerialized] private SplineArcLengthTable _arcA;
        [NonSerialized] private SplineArcLengthTable _arcB;

        public MeshCollider SourceSurface => sourceSurface;
        public SplineContainer SplineA => splineA;
        public SplineContainer SplineB => splineB;
        public bool ReverseSplineB => reverseSplineB;
        public int PointCount => pointCount;
        public bool ShowSceneHandles => showSceneHandles;
        public float HandleScreenSize => handleScreenSize;
        public bool AllowEndpointAdjustment => allowEndpointAdjustment;
        public bool ConstrainSplineKnotsToSurface =>
            constrainSplineKnotsToSurface;
        public bool AutoRebuildWhenSplinesChange =>
            autoRebuildWhenSplinesChange;

        private void Awake()
        {
            if (Application.isPlaying &&
                rebuildOnAwake)
            {
                Rebuild();
            }
        }

        private void OnValidate()
        {
            pointCount =
                Mathf.Max(2, pointCount);

            minimumPointGapMeters =
                Mathf.Max(
                    0.0001f,
                    minimumPointGapMeters);

            lengthSegmentsPerUnit =
                Mathf.Max(
                    1,
                    lengthSegmentsPerUnit);

            widthSegments =
                Mathf.Max(
                    1,
                    widthSegments);

            splineArcLengthResolution =
                Mathf.Max(
                    32,
                    splineArcLengthResolution);

            handleProjectionWindowSamples =
                Mathf.Max(
                    2,
                    handleProjectionWindowSamples);

            minimumProjectionCastDistance =
                Mathf.Max(
                    0.001f,
                    minimumProjectionCastDistance);

            knotSurfaceTolerance =
                Mathf.Max(
                    0.000001f,
                    knotSurfaceTolerance);

            EnsurePointSettings();
            InvalidateCaches();
        }

        [ContextMenu("Rebuild Lighting Meshes")]
        public void Rebuild()
        {
            ValidateInputs();

            EnsurePointSettings();
            RebuildArcLengthTables();
            ClampAllPointOffsets();

            MeshSurfaceProjector projector =
                new MeshSurfaceProjector(
                    sourceSurface,
                    minimumProjectionCastDistance);

            Transform root =
                GetOrCreateGeneratedRoot();

            ClearGeneratedChildren(root);

            int unitCount = pointCount - 1;

            for (int i = 0; i < unitCount; i++)
            {
                Mesh mesh =
                    BuildUnitMesh(i, projector);

                GameObject unitObject =
                    new GameObject(
                        $"LightingUnit_{i:00}");

                unitObject.transform.SetParent(
                    root,
                    false);

                MeshFilter filter =
                    unitObject.AddComponent<MeshFilter>();

                filter.sharedMesh = mesh;

                MeshRenderer renderer =
                    unitObject.AddComponent<MeshRenderer>();

                if (lightingMaterial != null)
                {
                    renderer.sharedMaterial =
                        lightingMaterial;
                }

                LightingSurfaceUnit unit =
                    unitObject.AddComponent<LightingSurfaceUnit>();

                unit.Initialize(i, renderer);
            }
        }

        [ContextMenu("Snap Spline Knots To Surface")]
        public bool SnapSplineKnotsToSurface()
        {
            if (sourceSurface == null)
                return false;

            MeshSurfaceProjector projector =
                new MeshSurfaceProjector(
                    sourceSurface,
                    minimumProjectionCastDistance);

            bool changed = false;

            changed |= SnapContainerKnots(
                splineA,
                projector);

            changed |= SnapContainerKnots(
                splineB,
                projector);

            if (changed)
            {
                InvalidateCaches();
            }

            return changed;
        }

        [ContextMenu("Reset Point Offsets")]
        public void ResetPointOffsets()
        {
            EnsurePointSettings();

            foreach (DivisionPointSetting setting
                     in pointSettings)
            {
                setting.splineAOffsetMeters = 0f;
                setting.splineBOffsetMeters = 0f;
            }

            InvalidateCaches();
        }

        [ContextMenu("Clear Generated Lighting Meshes")]
        public void ClearGenerated()
        {
            Transform root = FindGeneratedRoot();

            if (root != null)
                DestroyUnityObject(root.gameObject);
        }

        public void EnsurePointSettings()
        {
            pointSettings ??=
                new List<DivisionPointSetting>();

            while (pointSettings.Count < pointCount)
            {
                pointSettings.Add(
                    new DivisionPointSetting());
            }

            while (pointSettings.Count > pointCount)
            {
                pointSettings.RemoveAt(
                    pointSettings.Count - 1);
            }
        }

        public void RebuildArcLengthTables()
        {
            if (splineA == null ||
                splineB == null)
            {
                _arcA = null;
                _arcB = null;
                return;
            }

            _arcA =
                new SplineArcLengthTable(
                    splineA,
                    splineArcLengthResolution);

            _arcB =
                new SplineArcLengthTable(
                    splineB,
                    splineArcLengthResolution);
        }

        public void InvalidateCaches()
        {
            _arcA = null;
            _arcB = null;
        }

        public Vector3 GetPointWorld(
            SplineSide side,
            int pointIndex)
        {
            EnsureArcTables();

            float distance =
                GetPointLogicalDistance(
                    side,
                    pointIndex);

            return side == SplineSide.A
                ? _arcA.EvaluateWorld(
                    distance,
                    false)
                : _arcB.EvaluateWorld(
                    distance,
                    reverseSplineB);
        }

        public Vector3 GetPointTangentWorld(
            SplineSide side,
            int pointIndex)
        {
            EnsureArcTables();

            float distance =
                GetPointLogicalDistance(
                    side,
                    pointIndex);

            return side == SplineSide.A
                ? _arcA.EvaluateTangentWorld(
                    distance,
                    false)
                : _arcB.EvaluateTangentWorld(
                    distance,
                    reverseSplineB);
        }

        public float ProjectHandleWorldPositionToLogicalDistance(
            SplineSide side,
            int pointIndex,
            Vector3 draggedWorldPosition)
        {
            EnsureArcTables();

            float current =
                GetPointLogicalDistance(
                    side,
                    pointIndex);

            float projected =
                side == SplineSide.A
                    ? _arcA.ProjectWorldPointToLogicalDistance(
                        draggedWorldPosition,
                        current,
                        false,
                        handleProjectionWindowSamples)
                    : _arcB.ProjectWorldPointToLogicalDistance(
                        draggedWorldPosition,
                        current,
                        reverseSplineB,
                        handleProjectionWindowSamples);

            return ClampPointLogicalDistance(
                side,
                pointIndex,
                projected);
        }

        public void SetPointLogicalDistance(
            SplineSide side,
            int pointIndex,
            float requestedLogicalDistance)
        {
            ValidatePointIndex(pointIndex);
            EnsureArcTables();
            EnsurePointSettings();

            float clamped =
                ClampPointLogicalDistance(
                    side,
                    pointIndex,
                    requestedLogicalDistance);

            float baseDistance =
                GetBaseLogicalDistance(
                    side,
                    pointIndex);

            float offset =
                clamped - baseDistance;

            if (side == SplineSide.A)
            {
                pointSettings[pointIndex]
                    .splineAOffsetMeters = offset;
            }
            else
            {
                pointSettings[pointIndex]
                    .splineBOffsetMeters = offset;
            }
        }

        public float GetPointLogicalDistance(
            SplineSide side,
            int pointIndex)
        {
            ValidatePointIndex(pointIndex);
            EnsureArcTables();
            EnsurePointSettings();

            float baseDistance =
                GetBaseLogicalDistance(
                    side,
                    pointIndex);

            float offset =
                side == SplineSide.A
                    ? pointSettings[pointIndex]
                        .splineAOffsetMeters
                    : pointSettings[pointIndex]
                        .splineBOffsetMeters;

            return Mathf.Clamp(
                baseDistance + offset,
                0f,
                GetSplineLength(side));
        }

        public bool IsPointDraggable(
            int pointIndex)
        {
            ValidatePointIndex(pointIndex);

            if (allowEndpointAdjustment)
                return true;

            return pointIndex > 0 &&
                   pointIndex < pointCount - 1;
        }

        public int GetEditorStateHash()
        {
            unchecked
            {
                int hash = 17;

                hash =
                    hash * 31 +
                    GetContainerHash(splineA);

                hash =
                    hash * 31 +
                    GetContainerHash(splineB);

                hash =
                    hash * 31 +
                    (sourceSurface != null
                        ? sourceSurface.transform
                            .localToWorldMatrix
                            .GetHashCode()
                        : 0);

                return hash;
            }
        }

        private Mesh BuildUnitMesh(
            int unitIndex,
            MeshSurfaceProjector projector)
        {
            float aStart =
                GetPointLogicalDistance(
                    SplineSide.A,
                    unitIndex);

            float aEnd =
                GetPointLogicalDistance(
                    SplineSide.A,
                    unitIndex + 1);

            float bStart =
                GetPointLogicalDistance(
                    SplineSide.B,
                    unitIndex);

            float bEnd =
                GetPointLogicalDistance(
                    SplineSide.B,
                    unitIndex + 1);

            int longitudinalCount =
                lengthSegmentsPerUnit + 1;

            int widthCount =
                widthSegments + 1;

            int vertexCount =
                longitudinalCount * widthCount;

            var vertices =
                new List<Vector3>(vertexCount);

            var normals =
                new List<Vector3>(vertexCount);

            var uv0 =
                new List<Vector2>(vertexCount);

            var uv1 =
                new List<Vector2>(vertexCount);

            var triangles =
                new List<int>(
                    lengthSegmentsPerUnit *
                    widthSegments *
                    6);

            float lengthA =
                Mathf.Max(
                    _arcA.Length,
                    1e-6f);

            float lengthB =
                Mathf.Max(
                    _arcB.Length,
                    1e-6f);

            for (int lengthIndex = 0;
                 lengthIndex <= lengthSegmentsPerUnit;
                 lengthIndex++)
            {
                float u =
                    lengthIndex /
                    (float)lengthSegmentsPerUnit;

                float distanceA =
                    Mathf.Lerp(
                        aStart,
                        aEnd,
                        u);

                float distanceB =
                    Mathf.Lerp(
                        bStart,
                        bEnd,
                        u);

                Vector3 worldA =
                    _arcA.EvaluateWorld(
                        distanceA,
                        false);

                Vector3 worldB =
                    _arcB.EvaluateWorld(
                        distanceB,
                        reverseSplineB);

                Vector3 tangentA =
                    _arcA.EvaluateTangentWorld(
                        distanceA,
                        false);

                Vector3 tangentB =
                    _arcB.EvaluateTangentWorld(
                        distanceB,
                        reverseSplineB);

                Vector3 longitudinal =
                    tangentA + tangentB;

                if (longitudinal.sqrMagnitude <=
                    1e-12f)
                {
                    longitudinal =
                        tangentA.sqrMagnitude > 1e-12f
                            ? tangentA
                            : tangentB;
                }

                longitudinal.Normalize();

                Vector3 widthDirection =
                    worldA - worldB;

                Vector3 approximateNormal =
                    Vector3.Cross(
                        longitudinal,
                        widthDirection);

                if (approximateNormal.sqrMagnitude <=
                    1e-12f)
                {
                    approximateNormal =
                        transform.up;
                }

                approximateNormal.Normalize();

                float globalU =
                    0.5f *
                    ((distanceA / lengthA) +
                     (distanceB / lengthB));

                for (int widthIndex = 0;
                     widthIndex <= widthSegments;
                     widthIndex++)
                {
                    float v =
                        widthIndex /
                        (float)widthSegments;

                    Vector3 candidate =
                        Vector3.Lerp(
                            worldB,
                            worldA,
                            v);

                    MeshSurfaceProjector.Result projection =
                        projector.Project(
                            candidate,
                            approximateNormal);

                    Vector3 normal =
                        projection.Normal;

                    if (invertSurfaceOffsetNormal)
                        normal = -normal;

                    Vector3 finalWorld =
                        projection.Point +
                        normal *
                        surfaceOffsetMeters;

                    vertices.Add(
                        transform.InverseTransformPoint(
                            finalWorld));

                    normals.Add(
                        transform.InverseTransformDirection(
                            normal).normalized);

                    // Local lighting-unit UV.
                    // v=0 => Spline B/front/transparent
                    // v=1 => Spline A/back/opaque
                    uv0.Add(
                        new Vector2(u, v));

                    // Continuous longitudinal coordinate.
                    uv1.Add(
                        new Vector2(globalU, v));
                }
            }

            for (int lengthIndex = 0;
                 lengthIndex < lengthSegmentsPerUnit;
                 lengthIndex++)
            {
                for (int widthIndex = 0;
                     widthIndex < widthSegments;
                     widthIndex++)
                {
                    int row0 =
                        lengthIndex * widthCount;

                    int row1 =
                        (lengthIndex + 1) *
                        widthCount;

                    int v00 =
                        row0 + widthIndex;

                    int v01 =
                        row0 + widthIndex + 1;

                    int v10 =
                        row1 + widthIndex;

                    int v11 =
                        row1 + widthIndex + 1;

                    if (!flipTriangles)
                    {
                        triangles.Add(v00);
                        triangles.Add(v10);
                        triangles.Add(v01);

                        triangles.Add(v10);
                        triangles.Add(v11);
                        triangles.Add(v01);
                    }
                    else
                    {
                        triangles.Add(v00);
                        triangles.Add(v01);
                        triangles.Add(v10);

                        triangles.Add(v10);
                        triangles.Add(v01);
                        triangles.Add(v11);
                    }
                }
            }

            Mesh mesh = new Mesh
            {
                name =
                    $"LightingUnitMesh_{unitIndex:00}"
            };

            if (vertexCount > 65535)
            {
                mesh.indexFormat =
                    UnityEngine.Rendering
                        .IndexFormat.UInt32;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv0);
            mesh.SetUVs(1, uv1);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            return mesh;
        }

        private bool SnapContainerKnots(
            SplineContainer container,
            MeshSurfaceProjector projector)
        {
            if (container == null)
                return false;

            Spline spline = container.Spline;

            if (spline == null ||
                spline.Count == 0 ||
                spline.IsReadOnly)
            {
                return false;
            }

            bool changed = false;

            for (int i = 0; i < spline.Count; i++)
            {
                BezierKnot knot = spline[i];

                Vector3 local =
                    new Vector3(
                        knot.Position.x,
                        knot.Position.y,
                        knot.Position.z);

                Vector3 world =
                    container.transform
                        .TransformPoint(local);

                Vector3 snapped =
                    projector.SnapPoint(world);

                if (Vector3.Distance(
                        world,
                        snapped) <=
                    knotSurfaceTolerance)
                {
                    continue;
                }

                Vector3 snappedLocal =
                    container.transform
                        .InverseTransformPoint(
                            snapped);

                knot.Position =
                    new float3(
                        snappedLocal.x,
                        snappedLocal.y,
                        snappedLocal.z);

                spline[i] = knot;
                changed = true;
            }

            return changed;
        }

        private float ClampPointLogicalDistance(
            SplineSide side,
            int pointIndex,
            float requested)
        {
            ValidatePointIndex(pointIndex);

            float length =
                GetSplineLength(side);

            if (!allowEndpointAdjustment)
            {
                if (pointIndex == 0)
                    return 0f;

                if (pointIndex ==
                    pointCount - 1)
                {
                    return length;
                }
            }

            float min = 0f;
            float max = length;

            if (pointIndex > 0)
            {
                min =
                    GetPointLogicalDistance(
                        side,
                        pointIndex - 1) +
                    minimumPointGapMeters;
            }

            if (pointIndex <
                pointCount - 1)
            {
                max =
                    GetPointLogicalDistance(
                        side,
                        pointIndex + 1) -
                    minimumPointGapMeters;
            }

            if (min > max)
            {
                float middle =
                    0.5f * (min + max);

                min = middle;
                max = middle;
            }

            return Mathf.Clamp(
                requested,
                min,
                max);
        }

        private void ClampAllPointOffsets()
        {
            EnsurePointSettings();

            if (!allowEndpointAdjustment)
            {
                pointSettings[0]
                    .splineAOffsetMeters = 0f;

                pointSettings[0]
                    .splineBOffsetMeters = 0f;

                pointSettings[pointCount - 1]
                    .splineAOffsetMeters = 0f;

                pointSettings[pointCount - 1]
                    .splineBOffsetMeters = 0f;
            }

            ClampSide(SplineSide.A);
            ClampSide(SplineSide.B);
        }

        private void ClampSide(
            SplineSide side)
        {
            for (int i = 0;
                 i < pointCount;
                 i++)
            {
                float requested =
                    GetPointLogicalDistance(
                        side,
                        i);

                SetPointLogicalDistance(
                    side,
                    i,
                    requested);
            }
        }

        private float GetBaseLogicalDistance(
            SplineSide side,
            int pointIndex)
        {
            float length =
                GetSplineLength(side);

            return length *
                   (pointIndex /
                    (float)(pointCount - 1));
        }

        private float GetSplineLength(
            SplineSide side)
        {
            EnsureArcTables();

            return side == SplineSide.A
                ? _arcA.Length
                : _arcB.Length;
        }

        private void EnsureArcTables()
        {
            if (_arcA == null ||
                _arcB == null)
            {
                RebuildArcLengthTables();
            }

            if (_arcA == null ||
                _arcB == null)
            {
                throw new InvalidOperationException(
                    "Spline A and Spline B must both be assigned.");
            }
        }

        private void ValidateInputs()
        {
            if (sourceSurface == null)
            {
                throw new InvalidOperationException(
                    "Source Surface MeshCollider is not assigned.");
            }

            if (sourceSurface.sharedMesh == null)
            {
                throw new InvalidOperationException(
                    "Source Surface MeshCollider has no shared mesh.");
            }

            if (splineA == null ||
                splineB == null)
            {
                throw new InvalidOperationException(
                    "Spline A and Spline B must both be assigned.");
            }

            RebuildArcLengthTables();

            if (_arcA.Length <= 1e-6f ||
                _arcB.Length <= 1e-6f)
            {
                throw new InvalidOperationException(
                    "Both splines must have non-zero length.");
            }
        }

        private void ValidatePointIndex(
            int pointIndex)
        {
            if (pointIndex < 0 ||
                pointIndex >= pointCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pointIndex));
            }
        }

        private static int GetContainerHash(
            SplineContainer container)
        {
            if (container == null)
                return 0;

            unchecked
            {
                int hash =
                    container.transform
                        .localToWorldMatrix
                        .GetHashCode();

                Spline spline =
                    container.Spline;

                if (spline == null)
                    return hash;

                hash =
                    hash * 31 +
                    spline.Count;

                for (int i = 0;
                     i < spline.Count;
                     i++)
                {
                    hash =
                        hash * 31 +
                        spline[i].GetHashCode();
                }

                return hash;
            }
        }

        private Transform GetOrCreateGeneratedRoot()
        {
            Transform root =
                FindGeneratedRoot();

            if (root != null)
                return root;

            GameObject go =
                new GameObject(
                    string.IsNullOrWhiteSpace(
                        generatedRootName)
                        ? "__GeneratedLightingSurfaces"
                        : generatedRootName);

            go.transform.SetParent(
                transform,
                false);

            return go.transform;
        }

        private Transform FindGeneratedRoot()
        {
            string rootName =
                string.IsNullOrWhiteSpace(
                    generatedRootName)
                    ? "__GeneratedLightingSurfaces"
                    : generatedRootName;

            return transform.Find(rootName);
        }

        private static void ClearGeneratedChildren(
            Transform root)
        {
            for (int i = root.childCount - 1;
                 i >= 0;
                 i--)
            {
                Transform child =
                    root.GetChild(i);

                MeshFilter filter =
                    child.GetComponent<MeshFilter>();

                if (filter != null &&
                    filter.sharedMesh != null)
                {
                    DestroyUnityObject(
                        filter.sharedMesh);
                }

                DestroyUnityObject(
                    child.gameObject);
            }
        }

        private static void DestroyUnityObject(
            UnityEngine.Object obj)
        {
            if (obj == null)
                return;

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEngine.Object
                    .DestroyImmediate(obj);
                return;
            }
#endif

            UnityEngine.Object.Destroy(obj);
        }
    }
}
