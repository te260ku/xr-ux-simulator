using System;
using UnityEngine;
using UnityEngine.Splines;

namespace SplineRibbonLighting
{
    internal sealed class SplineArcLengthTable
    {
        private readonly SplineContainer spline;
        private readonly Vector3[] positions;
        private readonly float[] cumulativeDistances;

        public float Length { get; }
        public int Resolution => positions.Length - 1;

        public SplineArcLengthTable(SplineContainer spline, int resolution)
        {
            this.spline = spline != null ? spline : throw new ArgumentNullException(nameof(spline));
            resolution = Mathf.Max(16, resolution);
            positions = new Vector3[resolution + 1];
            cumulativeDistances = new float[resolution + 1];

            positions[0] = (Vector3)spline.EvaluatePosition(0f);
            float accumulated = 0f;
            for (int i = 1; i <= resolution; i++)
            {
                float t = i / (float)resolution;
                positions[i] = (Vector3)spline.EvaluatePosition(t);
                accumulated += Vector3.Distance(positions[i - 1], positions[i]);
                cumulativeDistances[i] = accumulated;
            }
            Length = accumulated;
        }

        public Vector3 EvaluateWorld(float logicalDistance, bool reverse)
        {
            float nativeDistance = LogicalToNativeDistance(logicalDistance, reverse);
            return (Vector3)spline.EvaluatePosition(NativeDistanceToT(nativeDistance));
        }

        public Vector3 EvaluateLogicalTangentWorld(float logicalDistance, bool reverse)
        {
            if (Length <= 1e-6f) return Vector3.forward;
            float step = Mathf.Max(Length / Resolution, 0.0001f);
            float d0 = Mathf.Clamp(logicalDistance - step, 0f, Length);
            float d1 = Mathf.Clamp(logicalDistance + step, 0f, Length);
            Vector3 tangent = EvaluateWorld(d1, reverse) - EvaluateWorld(d0, reverse);
            return tangent.sqrMagnitude > 1e-12f ? tangent.normalized : Vector3.forward;
        }

        public float ProjectWorldPointToLogicalDistance(Vector3 worldPoint, float currentLogicalDistance, bool reverse, int searchWindowSamples)
        {
            if (Length <= 1e-6f) return 0f;
            float currentNative = LogicalToNativeDistance(currentLogicalDistance, reverse);
            int centerIndex = Mathf.Clamp(Mathf.RoundToInt(NativeDistanceToT(currentNative) * Resolution), 0, Resolution);
            searchWindowSamples = Mathf.Clamp(searchWindowSamples, 2, Resolution);
            int first = Mathf.Max(0, centerIndex - searchWindowSamples);
            int last = Mathf.Min(Resolution - 1, centerIndex + searchWindowSamples);

            float bestSqr = float.PositiveInfinity;
            float bestNativeDistance = currentNative;
            for (int segment = first; segment <= last; segment++)
            {
                Vector3 a = positions[segment];
                Vector3 b = positions[segment + 1];
                float u = ClosestPointParameter(worldPoint, a, b);
                Vector3 closest = Vector3.LerpUnclamped(a, b, u);
                float sqr = (worldPoint - closest).sqrMagnitude;
                if (sqr >= bestSqr) continue;
                bestSqr = sqr;
                float segmentLength = cumulativeDistances[segment + 1] - cumulativeDistances[segment];
                bestNativeDistance = cumulativeDistances[segment] + segmentLength * u;
            }
            return NativeToLogicalDistance(bestNativeDistance, reverse);
        }

        private float LogicalToNativeDistance(float logicalDistance, bool reverse)
        {
            logicalDistance = Mathf.Clamp(logicalDistance, 0f, Length);
            return reverse ? Length - logicalDistance : logicalDistance;
        }

        private float NativeToLogicalDistance(float nativeDistance, bool reverse)
        {
            nativeDistance = Mathf.Clamp(nativeDistance, 0f, Length);
            return reverse ? Length - nativeDistance : nativeDistance;
        }

        private float NativeDistanceToT(float nativeDistance)
        {
            if (Length <= 1e-6f) return 0f;
            nativeDistance = Mathf.Clamp(nativeDistance, 0f, Length);
            int low = 0;
            int high = cumulativeDistances.Length - 1;
            while (low < high)
            {
                int mid = (low + high) / 2;
                if (cumulativeDistances[mid] < nativeDistance) low = mid + 1;
                else high = mid;
            }
            int upper = Mathf.Clamp(low, 1, Resolution);
            int lower = upper - 1;
            float d0 = cumulativeDistances[lower];
            float d1 = cumulativeDistances[upper];
            float local = Mathf.Approximately(d0, d1) ? 0f : Mathf.InverseLerp(d0, d1, nativeDistance);
            return Mathf.LerpUnclamped(lower / (float)Resolution, upper / (float)Resolution, local);
        }

        private static float ClosestPointParameter(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float denominator = Vector3.Dot(ab, ab);
            if (denominator <= 1e-12f) return 0f;
            return Mathf.Clamp01(Vector3.Dot(point - a, ab) / denominator);
        }
    }
}
