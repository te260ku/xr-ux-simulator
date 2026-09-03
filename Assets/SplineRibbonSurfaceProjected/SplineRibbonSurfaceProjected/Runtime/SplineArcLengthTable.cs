using System;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRibbonLightingNew
{
    /// <summary>
    /// Approximate world-space arc-length lookup table.
    /// Point positions are stored as physical distance along the spline
    /// instead of raw spline t.
    /// </summary>
    internal sealed class SplineArcLengthTable
    {
        private readonly SplineContainer _container;
        private readonly Vector3[] _positions;
        private readonly float[] _cumulativeDistances;

        public float Length { get; }
        public int Resolution => _positions.Length - 1;

        public SplineArcLengthTable(
            SplineContainer container,
            int resolution)
        {
            _container = container != null
                ? container
                : throw new ArgumentNullException(nameof(container));

            resolution = Mathf.Max(32, resolution);

            _positions = new Vector3[resolution + 1];
            _cumulativeDistances = new float[resolution + 1];

            _positions[0] =
                (Vector3)_container.EvaluatePosition(0f);

            float accumulated = 0f;

            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;

                _positions[i] =
                    (Vector3)_container.EvaluatePosition(t);

                accumulated += Vector3.Distance(
                    _positions[i - 1],
                    _positions[i]);

                _cumulativeDistances[i] = accumulated;
            }

            Length = accumulated;
        }

        public Vector3 EvaluateWorld(
            float logicalDistance,
            bool reverse)
        {
            float nativeDistance =
                LogicalToNativeDistance(
                    logicalDistance,
                    reverse);

            float t = NativeDistanceToT(nativeDistance);

            return (Vector3)_container.EvaluatePosition(t);
        }

        public Vector3 EvaluateTangentWorld(
            float logicalDistance,
            bool reverse)
        {
            if (Length <= 1e-6f)
                return Vector3.forward;

            float step =
                Mathf.Max(Length / Resolution, 0.0001f);

            float d0 =
                Mathf.Clamp(
                    logicalDistance - step,
                    0f,
                    Length);

            float d1 =
                Mathf.Clamp(
                    logicalDistance + step,
                    0f,
                    Length);

            Vector3 p0 = EvaluateWorld(d0, reverse);
            Vector3 p1 = EvaluateWorld(d1, reverse);

            Vector3 tangent = p1 - p0;

            return tangent.sqrMagnitude > 1e-12f
                ? tangent.normalized
                : Vector3.forward;
        }

        public float ProjectWorldPointToLogicalDistance(
            Vector3 worldPoint,
            float currentLogicalDistance,
            bool reverse,
            int searchWindowSamples)
        {
            if (Length <= 1e-6f)
                return 0f;

            float currentNative =
                LogicalToNativeDistance(
                    currentLogicalDistance,
                    reverse);

            int centerIndex =
                Mathf.Clamp(
                    Mathf.RoundToInt(
                        NativeDistanceToT(currentNative) *
                        Resolution),
                    0,
                    Resolution);

            searchWindowSamples =
                Mathf.Clamp(
                    searchWindowSamples,
                    2,
                    Resolution);

            int first =
                Mathf.Max(
                    0,
                    centerIndex - searchWindowSamples);

            int last =
                Mathf.Min(
                    Resolution - 1,
                    centerIndex + searchWindowSamples);

            float bestSqr = float.PositiveInfinity;
            float bestNativeDistance = currentNative;

            for (int i = first; i <= last; i++)
            {
                Vector3 a = _positions[i];
                Vector3 b = _positions[i + 1];

                float segmentT =
                    ClosestPointParameter(
                        worldPoint,
                        a,
                        b);

                Vector3 closest =
                    Vector3.LerpUnclamped(
                        a,
                        b,
                        segmentT);

                float sqr =
                    (worldPoint - closest).sqrMagnitude;

                if (sqr >= bestSqr)
                    continue;

                bestSqr = sqr;

                float d0 =
                    _cumulativeDistances[i];

                float d1 =
                    _cumulativeDistances[i + 1];

                bestNativeDistance =
                    Mathf.LerpUnclamped(
                        d0,
                        d1,
                        segmentT);
            }

            return NativeToLogicalDistance(
                bestNativeDistance,
                reverse);
        }

        private float LogicalToNativeDistance(
            float logicalDistance,
            bool reverse)
        {
            logicalDistance =
                Mathf.Clamp(
                    logicalDistance,
                    0f,
                    Length);

            return reverse
                ? Length - logicalDistance
                : logicalDistance;
        }

        private float NativeToLogicalDistance(
            float nativeDistance,
            bool reverse)
        {
            nativeDistance =
                Mathf.Clamp(
                    nativeDistance,
                    0f,
                    Length);

            return reverse
                ? Length - nativeDistance
                : nativeDistance;
        }

        private float NativeDistanceToT(
            float nativeDistance)
        {
            if (Length <= 1e-6f)
                return 0f;

            nativeDistance =
                Mathf.Clamp(
                    nativeDistance,
                    0f,
                    Length);

            int low = 0;
            int high = Resolution;

            while (low < high)
            {
                int mid = (low + high) / 2;

                if (_cumulativeDistances[mid] <
                    nativeDistance)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            int upper =
                Mathf.Clamp(low, 1, Resolution);

            int lower = upper - 1;

            float d0 =
                _cumulativeDistances[lower];

            float d1 =
                _cumulativeDistances[upper];

            float segmentT =
                Mathf.Approximately(d0, d1)
                    ? 0f
                    : Mathf.InverseLerp(
                        d0,
                        d1,
                        nativeDistance);

            float t0 =
                lower / (float)Resolution;

            float t1 =
                upper / (float)Resolution;

            return Mathf.LerpUnclamped(
                t0,
                t1,
                segmentT);
        }

        private static float ClosestPointParameter(
            Vector3 point,
            Vector3 a,
            Vector3 b)
        {
            Vector3 ab = b - a;

            float denominator =
                Vector3.Dot(ab, ab);

            if (denominator <= 1e-12f)
                return 0f;

            return Mathf.Clamp01(
                Vector3.Dot(point - a, ab) /
                denominator);
        }
    }
}
