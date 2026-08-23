# Timeline hierarchy self-repair fix

## Target error

The following Play Mode initialization error was addressed:

```text
Timeline UI hierarchy is missing. UI is no longer generated at runtime. Run Tools > Lighting Scenario > Build Complete UI In Current Scene.
UnityEngine.Debug:LogError (object,UnityEngine.Object)
LightingScenarioTool.TimelinePanel:Initialize (...)
```

## Root cause

`TimelinePanel.Initialize()` treated the scene-authored Timeline hierarchy as mandatory at runtime. `BindExistingChrome()` could recover references only when all required objects already existed. If one viewport, content object, scrollbar, or template was missing, the component disabled itself instead of repairing the missing piece.

This was especially fragile for partially migrated scenes: the application-level UI hierarchy could still be valid enough for `LightingScenarioApp` to start, while the internal `TimelinePanel` hierarchy was incomplete.

## Implementation changes

### 1. Runtime self-repair in `TimelinePanel`

`TimelinePanel.Initialize()` now:

1. binds existing serialized/name-based references;
2. repairs only missing required Timeline internals when binding is incomplete;
3. continues initialization when repair succeeds;
4. logs a warning rather than the previous fatal error when runtime repair was necessary.

The fallback can repair:

- `RulerViewport` / `RulerContent`
- `LabelsViewport` / `LabelsContent`
- `TimeViewport` / `TimeContent`
- `MarqueeSelection`
- `HorizontalScrollbar` and its handle hierarchy
- `TimelineTemplates`
- missing `TrackLabelTemplate`
- missing or incomplete `TrackTimeTemplate` / `ColorKeyframeLane`
- missing or incomplete `ColorKeyframeTemplate` visual components

### 2. Non-destructive repair policy

Existing authored Timeline UI is preserved where possible. Optional children of an existing `TrackLabelTemplate` (name field, Lock/Mute controls, move buttons, etc.) are not recreated merely because a designer removed or customized them. Only components dereferenced unconditionally by runtime code are treated as required.

The decorative section header/corner/lower-left area is recreated only when no recognizable Timeline chrome exists.

### 3. `Rebuild()` guard

`TimelinePanel.Rebuild()` now validates and repairs the hierarchy before clearing or rebuilding dynamic rows. This prevents a partially missing hierarchy from turning into a later `NullReferenceException` after initialization.

### 4. Keyframe template self-repair

`ColorKeyframeView` now exposes runtime-safe visual validation/repair. Missing `Image`, `Outline`, or selection-ring data can be reconstructed. Existing authored transform settings are preserved unless the visual is effectively unconfigured.

### 5. Editor repair persistence

`EditorRepairChrome()` uses the same repair path as runtime and treats missing serialized references as a scene change. The Editor helper/log messages were updated to document that both Timeline and Preview have runtime fallback repair.

## Validation performed

- Checked all 18 C# files for balanced braces/brackets/parentheses and `#if` / `#endif` structure.
- Confirmed the previous `Timeline UI hierarchy is missing...` runtime error string is no longer present in runtime source.
- Verified the supplied demo scene still contains serialized Timeline references and templates.
- Reviewed repair behavior for empty, partially missing, and complete Timeline hierarchy states.

## Not executed in this environment

Unity Editor is not available here, so Unity compilation and Play Mode execution were not run. After replacing the files, open the project and enter Play Mode. If runtime repair occurs, a warning is expected once; run **Tools > Lighting Scenario > Build Complete UI In Current Scene** outside Play Mode to persist the repaired hierarchy into the scene.
