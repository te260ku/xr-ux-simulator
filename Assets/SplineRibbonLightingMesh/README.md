# Spline Ribbon Lighting Mesh

## What it does

- Two non-intersecting `SplineContainer`s define the two sides of a ribbon surface.
- `Point Count = N` creates numbered boundary points `0..N-1` on both splines.
- Adjacent numbered boundaries create `N-1` independent lighting meshes.
- A/B points with the same number may be adjusted independently along their spline.
- Scene View displays numbered `A0/B0`, `A1/B1`, ... handles.
- Dragging a handle updates its distance along the spline and rebuilds all meshes immediately.

## Point representation

Point positions are stored as meter offsets from equal-distance base positions, not as raw spline `t` values.
Positive offset means toward the next point number. `Reverse Spline B` makes this logical direction consistent even if B was authored backwards.

By default the first and last points are fixed. Neighboring points cannot cross because `Minimum Point Gap Meters` is enforced.

## Generated mesh / shader contract

Every lighting unit uses:

- UV0.x = 0..1 inside that unit along the spline direction.
- UV0.y = 1 at Spline A (back / strongest illumination).
- UV0.y = 0 at Spline B (front / weakest illumination).
- UV1.x = continuous longitudinal progress across the whole ribbon.

Therefore point adjustment changes geometry without changing the meaning of the attenuation shader.

All generated `MeshRenderer`s use the same shared Material. `LightingSurfaceUnit` changes `_LightColor`, `_Intensity`, and `_FalloffPower` with `MaterialPropertyBlock`, so per-unit light control does not instantiate materials.

## Setup

1. Install Unity Splines.
2. Copy `Runtime`, `Editor`, and optionally `Shaders` into `Assets`.
3. Add `SplineRibbonMeshGenerator` to an empty GameObject.
4. Assign Spline A and B.
5. Enable `Reverse Spline B` if the splines have opposite native directions.
6. Assign a material using your lighting shader, or the included URP sample shader.
7. Select the generator in Scene View and drag the numbered handles.
8. If the surface is culled from the desired side, enable `Flip Normals`.

## Geometry model

The interior is a ruled surface. At every longitudinal sample, one point on A is connected by a straight cross-width line to the corresponding point on B. Two boundary splines alone do not define independent width-direction curvature.

## Notes

`SplineContainer.EvaluatePosition(float)` is used as a world-space curve evaluation. Arc length is approximated by a configurable lookup table (`Spline Arc Length Resolution`, default 512).
The handle projection search is local around the current point to reduce jumps when a spline folds close to itself.
