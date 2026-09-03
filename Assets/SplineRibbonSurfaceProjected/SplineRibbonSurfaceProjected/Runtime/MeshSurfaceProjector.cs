using System;
using UnityEngine;

namespace SplineRibbonLightingNew
{
    /// <summary>
    /// Projects candidate ribbon vertices onto a MeshCollider surface.
    ///
    /// Primary projection uses two ray casts from opposite sides of an
    /// approximate surface normal. This uses MeshCollider's acceleration
    /// structure and avoids scanning every source triangle for every vertex.
    ///
    /// Collider.ClosestPoint is used as a fallback.
    /// </summary>
    internal sealed class MeshSurfaceProjector
    {
        internal readonly struct Result
        {
            public readonly Vector3 Point;
            public readonly Vector3 Normal;
            public readonly bool UsedRaycast;

            public Result(
                Vector3 point,
                Vector3 normal,
                bool usedRaycast)
            {
                Point = point;
                Normal = normal;
                UsedRaycast = usedRaycast;
            }
        }

        private readonly MeshCollider _surface;
        private readonly float _castDistance;

        public MeshSurfaceProjector(
            MeshCollider surface,
            float minimumCastDistance)
        {
            _surface = surface != null
                ? surface
                : throw new ArgumentNullException(nameof(surface));

            float boundsDistance =
                Mathf.Max(
                    surface.bounds.extents.magnitude * 2f,
                    0.1f);

            _castDistance =
                Mathf.Max(
                    boundsDistance,
                    minimumCastDistance);
        }

        public Result Project(
            Vector3 candidateWorld,
            Vector3 approximateNormalWorld)
        {
            Vector3 normal =
                approximateNormalWorld.sqrMagnitude > 1e-12f
                    ? approximateNormalWorld.normalized
                    : Vector3.up;

            bool hitPositive =
                CastFromSide(
                    candidateWorld,
                    normal,
                    out RaycastHit positiveHit);

            bool hitNegative =
                CastFromSide(
                    candidateWorld,
                    -normal,
                    out RaycastHit negativeHit);

            if (hitPositive && hitNegative)
            {
                float positiveDistance =
                    Vector3.SqrMagnitude(
                        positiveHit.point -
                        candidateWorld);

                float negativeDistance =
                    Vector3.SqrMagnitude(
                        negativeHit.point -
                        candidateWorld);

                RaycastHit selected =
                    positiveDistance <= negativeDistance
                        ? positiveHit
                        : negativeHit;

                return new Result(
                    selected.point,
                    selected.normal.normalized,
                    true);
            }

            if (hitPositive)
            {
                return new Result(
                    positiveHit.point,
                    positiveHit.normal.normalized,
                    true);
            }

            if (hitNegative)
            {
                return new Result(
                    negativeHit.point,
                    negativeHit.normal.normalized,
                    true);
            }

            Vector3 closest =
                _surface.ClosestPoint(candidateWorld);

            return new Result(
                closest,
                normal,
                false);
        }

        public Vector3 SnapPoint(
            Vector3 worldPoint)
        {
            return _surface.ClosestPoint(worldPoint);
        }

        private bool CastFromSide(
            Vector3 candidateWorld,
            Vector3 sideNormal,
            out RaycastHit hit)
        {
            Vector3 origin =
                candidateWorld +
                sideNormal * _castDistance;

            Ray ray =
                new Ray(
                    origin,
                    -sideNormal);

            return _surface.Raycast(
                ray,
                out hit,
                _castDistance * 2f);
        }
    }
}
