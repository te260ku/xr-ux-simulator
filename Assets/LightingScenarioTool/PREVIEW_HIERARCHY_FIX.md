# Preview hierarchy error fix

## Target errors

- `Preview UI hierarchy is missing. UI is no longer generated at runtime.`
- `Preview hierarchy/templates are not configured.`

## Root cause

`PreviewPanel` previously treated its internal scene-authored UI hierarchy as mandatory at runtime. The Editor repair path only rebuilt the Preview chrome when the panel had **no children at all**. A partially missing hierarchy therefore passed the application-level UI binding but failed inside `PreviewPanel.Initialize()`, after which `OnDocumentChanged()` called `PreviewPanel.Rebuild()` and produced the second error.

## Changes

- Added idempotent Preview self-repair via `EnsureChrome()`.
- Missing Preview parts are created individually rather than replacing the whole panel:
  - Preview toolbar and required controls
  - `PreviewContent`
  - `PreviewBackgroundImage`
  - `LightsLayer`
  - `PreviewTemplates/PreviewLightTemplate`
- `EditorRepairChrome()` now repairs partially missing Preview hierarchies, not only completely empty ones.
- Added validation and repair for `PreviewLightView` visual dependencies (`Image`, `Outline`, label background, TMP label).
- `Rebuild()` retries Preview hierarchy repair before reporting a configuration error.
- Removed the unsafe fallback that could bind an arbitrary last TMP label as the Light Size value label.
- Kept the application shell scene-authored. Runtime creation is limited to a fallback for missing internal Preview children.

## Expected behavior

For an existing scene with an incomplete Preview hierarchy:

1. In Edit Mode, `Tools > Lighting Scenario > Build Complete UI In Current Scene` repairs and persists missing Preview children.
2. If an incomplete scene still reaches Play Mode, `PreviewPanel` repairs the missing internal children automatically instead of disabling itself.
3. The two target errors should no longer occur for repairable Preview hierarchy damage.

## Verification performed here

- Static delimiter and preprocessor-balance scan over all 18 C# files: passed.
- Confirmed the two previous Preview fatal-error strings are no longer present in active C# source.
- Unity Editor compilation / Play Mode execution was not available in this environment and must be verified in the target Unity project.
