# UI Generation Persistence Fix

## Problem

After running `Tools > Lighting Scenario > Build Complete UI In Current Scene` in Edit Mode and entering Play Mode, `TimelinePanel.Initialize()` and `PreviewPanel.Initialize()` could report that their generated UI hierarchy was incomplete and had been repaired at runtime.

The complete-build command generated the child hierarchy and populated serialized references on `TimelinePanel`, `PreviewPanel`, and template/view components. However, only the root `LightingScenarioApp` was explicitly marked dirty after the build. Child component reference changes therefore were not explicitly guaranteed to be persisted by the editor workflow. In addition, a successful runtime fallback repair was logged as a warning even though the UI could continue normally.

## Changes

- After complete UI generation, `EditorRepairUiHierarchy()` is executed immediately in Edit Mode as a validation/finalization pass.
- Every GameObject and Component below `LightingScenarioApp` is explicitly marked dirty after generation so child serialized references are persisted with the Scene.
- If pre-Play validation repairs anything, the whole application hierarchy is marked dirty rather than only the root component.
- `TimelinePanel.Initialize()` no longer emits a warning when its fallback repair succeeds.
- `PreviewPanel.Initialize()` no longer emits a warning when its fallback repair succeeds.
- Unrecoverable hierarchy failures still emit `Debug.LogError` and disable the affected panel.

## Expected behavior

1. In Edit Mode, run `Tools > Lighting Scenario > Build Complete UI In Current Scene`.
2. Enter Play Mode.
3. No `Timeline UI hierarchy was incomplete...` warning is emitted.
4. No `Preview UI hierarchy was incomplete...` warning is emitted.
5. Timeline and Preview continue to initialize normally.

## Validation performed

- 18 C# files checked for balanced braces/parentheses/brackets.
- Confirmed only one `MenuItem` remains under the tool source.
- Confirmed the previous runtime-repair warning strings no longer exist.

Unity Editor compilation and Play Mode execution cannot be run in this environment, so those checks should still be performed after importing the updated files.
