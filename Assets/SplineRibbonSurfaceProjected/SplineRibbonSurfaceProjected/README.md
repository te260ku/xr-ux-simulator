# Spline Ribbon Lighting - Surface Projected Version

This version combines the requirements discussed so far:

- Two manually-authored, non-intersecting Unity splines.
- Numbered A/B boundary points adjustable along their splines in Scene View.
- One independent lighting mesh per adjacent pair of numbered points.
- Generated lighting meshes conform to an existing curved mesh.
- UV0.y always means front/back lighting direction.
- Transparent HDR lighting shader with URP Bloom-compatible output.
- Optional Edit Mode constraint that snaps manually edited spline knots back to
  the reference surface.

## Required source components

The existing curved surface needs a MeshCollider.

Recommended hierarchy:

ReferenceSurface
- MeshFilter
- MeshRenderer
- MeshCollider

SplineA
- SplineContainer

SplineB
- SplineContainer

LightingSurfaceGenerator
- SplineRibbonMeshGenerator

## Generator setup

Assign:

- Source Surface: reference MeshCollider
- Spline A
- Spline B
- Lighting Material

If B was authored in the opposite direction, enable Reverse Spline B.

## Surface fitting

Each lighting unit is generated as a 2D grid.

Length Segments Per Unit:
controls resolution along the spline direction.

Width Segments:
controls resolution from Spline B to Spline A.

For each grid point:

1. Evaluate corresponding points on Spline A/B.
2. Linearly interpolate a candidate point between B and A.
3. Estimate a surface normal from spline tangent x width direction.
4. Raycast the MeshCollider from both normal directions.
5. Choose the hit nearest the candidate.
6. Fall back to Collider.ClosestPoint if neither ray hits.
7. Add Surface Offset along the hit normal.

This means the final lighting surface follows the existing curved mesh rather
than remaining a ruled surface between the splines.

## Z-fighting

Set Surface Offset Meters to a small positive value such as 0.001 for a model
whose Unity units are meters.

If the mesh moves into the reference surface instead of away from it, enable
Invert Surface Offset Normal.

## UV contract

UV0 is local to each lighting unit:

- UV0.x = 0..1 along the unit.
- UV0.y = 0 at Spline B / front.
- UV0.y = 1 at Spline A / back.

Therefore the transparent lighting shader is independent from the physical
curvature of the generated mesh.

## Manual spline editing

Unity's spline authoring tools can be used normally.

Constrain Spline Knots To Surface:
When enabled, the editor watcher detects changes to either SplineContainer,
snaps knot positions to Source Surface, then rebuilds the lighting meshes.

Important:
Only knot positions are constrained. A cubic Bezier segment between knots can
still deviate slightly from the reference surface. The final generated lighting
mesh is nevertheless projected vertex-by-vertex, so the rendered lighting
surface still conforms to the reference mesh.

If visual spline conformity itself must be exact, add more spline knots or use
Linear tangent mode in areas where that is appropriate.

## Numbered handles

Select the generator object.

Scene View shows:

A0, A1, A2...
B0, B1, B2...

Dragging a numbered handle:

- moves along the current spline tangent,
- projects the dragged result back to that spline,
- stores the position as an arc-distance offset in meters,
- prevents point-order inversion,
- immediately rebuilds all lighting meshes.

A_i and B_i are independent. This allows the boundary between two lighting
units to be angled.

## Shader

Use:

SplineRibbonLighting/TransparentLightingSurfaceBloom

The shader expects:

- Spline A/back: opaque
- Spline B/front: transparent

`_LightColor` is HDR and `_Intensity` can push RGB above 1.0 so URP Bloom can
create a luminous halo.

Material instances do not need to be duplicated. Each generated unit contains
LightingSurfaceUnit, which updates per-unit parameters with
MaterialPropertyBlock.

## Performance note

The final surface projection uses MeshCollider.Raycast, not a full triangle
scan for every generated vertex. This is appropriate for edit-time iteration.

Very high Width Segments / Length Segments values will still increase rebuild
cost, so start around 8 x 8 and increase only where curvature requires it.
