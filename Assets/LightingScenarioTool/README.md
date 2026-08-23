# Lighting Scenario Tool

Scene-authored uGUI / TextMesh Pro editor for authoring lighting scenarios in Unity.

## Current model

Each Lighting Unit owns one Lighting Track containing Color Keyframes only. There is no clip concept.
Lighting color is linearly interpolated between keyframes; before the first keyframe the output is Black,
and after the last keyframe the final color is held. Muted tracks evaluate to Black.

## UI layout

The scene-authored UI is organized into five areas:

1. **Menu Bar** - File / View / Help / Fullscreen / Exit
2. **Project / Scenario Bar** - Scenario name, Duration, current project filename
3. **Timeline Toolbar** - transport, current time, Loop, Snap
4. **Selection Inspector** - shown only while Color Keyframes are selected
5. **Workspace** - Timeline on the left and Preview on the right

### File menu

- New
- Open...
- Save
- Save As...
- Export...
- Exit

The project indicator displays only the JSON filename (or `Untitled`), with `*` for unsaved changes.
The full path is not kept on screen.

## Color Keyframe editing

- Double-click an empty position on a timeline track to create a Color Keyframe.
- Click a diamond to select it.
- Ctrl-click to add/remove keyframes from a multi-selection.
- `Ctrl + C` copies the selected Lighting Unit in Preview. `Ctrl + V` creates a clean copy with Lock=false, Mute=false, and no Color Keyframes. `Ctrl + Shift + V` pastes the complete copied Unit including Lock, Mute, and all Color Keyframes. `Ctrl + D` duplicates the selected Unit completely. New Units receive new IDs, and full-copy keyframes receive new Keyframe IDs.
- Drag selected diamonds to move the selection together while preserving relative spacing.
- Right-click a keyframe for delete; keyboard Delete also works.
- The Selection Inspector shows exact Time and a Color swatch.
- Click the swatch (or double-click a keyframe) to open the HSV color picker.
- The picker contains a Hue Ring, Saturation-Value square, RGB fields, HSV fields, and current-color preview.
- Keyframe fill uses the keyframe color. Selection uses a fixed Accent-color outer ring with a 2 px gap from the keyframe body; the ring color does not change based on the keyframe color.
- Adjacent keyframes are connected by an approximate color-gradient strip.
- Track Lock prevents keyframe edits. Track Mute affects preview only.

## Timeline

- Track names remain fixed on the left while only the time area scrolls horizontally.
- Selected tracks are highlighted in both the fixed track list and time area.
- Ruler/grid major intervals adapt to zoom level.
- The Playhead has a vertical line and draggable head.
- Mouse wheel: vertical track scroll while the ruler remains fixed.
- Ctrl + mouse wheel: zoom time axis.
- Middle mouse button + horizontal drag: horizontal pan.
- Bottom horizontal scrollbar: horizontal scroll.

## Preview

- Preview-specific controls live in the Preview toolbar: **Background**, **Light Size**, and **Unit Names** visibility.
- Right-click empty preview space to add a Lighting Unit.
- Drag a Lighting Unit to reposition it.
- Click empty preview space to clear selection.
- Lighting Unit names are displayed directly above their square and can be shown/hidden with the **Unit Names** checkbox.
- Selected units receive an outline, and the matching Timeline track is highlighted.
- Background image path, Light Size, and Unit Names visibility are saved in project JSON and restored on load. Light Size is limited to 20-40.
- Background images preserve aspect ratio and are contained within the Preview area.

## Project save behavior

- Project JSON is written only when Save or Save As is explicitly executed.
- A new project has no current path and displays `Untitled`.
- Save overwrites the current project file; when no path exists it behaves as Save As.
- Save As always opens a file picker for destination folder and JSON filename.
- Open loads the selected JSON and makes it the current project file.
- New / Open / Exit with unsaved changes shows Save / Don't Save / Cancel.
- Project JSON is never automatically redirected to `Application.persistentDataPath` / AppData.

## Shortcuts

- `Ctrl + N`: Create a new project
- `Ctrl + O`: Open an existing project
- `Ctrl + S`: Save the current project (opens Save As when no current project path exists)
- `Ctrl + Z`: Undo
- `Ctrl + Shift + Z`: Redo
- `Ctrl + C`: Copy selected Color Keyframes, or the selected Lighting Unit when no Keyframe is selected
- `Ctrl + V`: Paste copied Color Keyframes into the currently selected Lighting Track, or paste a copied Lighting Unit without Lock/Mute/Color Keyframes
- `Ctrl + Shift + V`: For a copied Lighting Unit, paste Lock/Mute/Color Keyframes as well
- `Ctrl + D`: Duplicate selected Color Keyframes, or fully duplicate the selected Lighting Unit
- `Delete`: Delete selected Color Keyframes, or the selected Lighting Unit if no keyframe is selected
- `Home`: Jump to start
- `End`: Jump to end
- `Space`: Play / Pause
- `Shift + Space`: Stop
- `Esc`: Stop, or close an open popup

## Data compatibility

The current data format version is `3.0.0`. Track data stores Color Keyframes only.
Unknown fields from older JSON files are ignored when loading and are not written back by the current model.

## Color presets

The HSV color picker can save the currently selected color with **Save Preset**.
Saved presets are shown as swatches in the picker and can be clicked to apply the color to the selected Color Keyframe(s).
Right-click a preset swatch and choose **Delete Preset** to remove it. Clicking outside the preset context menu closes only that menu and keeps the color picker open.
Presets are stored as application-level preferences (PlayerPrefs), are shared across project files, and persist between launches. Up to 12 presets are retained; saving an existing color moves it to the front instead of creating a duplicate.

### Marquee selection / Exit

- Drag on an empty part of the Timeline time area with the left mouse button to marquee-select multiple Color Keyframes.
- Hold Ctrl while marquee-dragging to add the enclosed keyframes to the current selection.
- Dragging a Color Keyframe itself continues to move the selected keyframe(s), not start marquee selection.
- `Fullscreen` / `Windowed` and `Exit` buttons are available at the upper-right of the Menu Bar. The fullscreen button switches between borderless fullscreen and the previous windowed size. Exit uses the same unsaved-changes confirmation as File > Exit.

## Windows standalone file dialogs

- Editor uses `UnityEditor.EditorUtility` file panels.
- Windows standalone builds use the native Unicode `GetOpenFileNameW` / `GetSaveFileNameW` common dialogs.
- The `OPENFILENAMEW` string buffers are allocated as unmanaged writable memory instead of marshalling `StringBuilder` fields. This is intended to behave consistently under both Mono and IL2CPP player builds.
- If the native dialog fails at the OS level, the application status displays the `CommDlgExtendedError` code. Closing/cancelling a dialog normally does not display an error.

## UI visual-system update

The default Scene UI and generated runtime Prefab Assets are initially created from the shared `AppTheme` / `UiFactory` visual system:

- Background / Panel / Elevated color hierarchy
- one Accent color for selection, focus, active controls and keyframe selection
- dedicated Playhead color
- 4/8/12/16/24 px spacing tokens
- 13 px body, 12 px secondary, 11 px timeline-scale and 15 px section-title typography
- consistent 32 px controls with common hover / pressed / selected / disabled states
- Scenario Header with labels separated from editable values
- stable Keyframe Editor whose controls are disabled when no keyframe is selected
- Timeline and Preview section headers with a dedicated pane divider
- wider Track Name editing area, explicit Lock / Mute controls, Accent track selection
- keyframe data color kept separate from the fixed Accent selection ring, with visible spacing between the body and ring
- Preview toolbar with non-truncated Background / Light Size controls
- Preview unit-name badges for readability over background imagery

These changes are visual/layout changes only; project persistence, timeline editing, shortcuts,
Windows native file dialogs, color presets, marquee selection, and Preview interactions remain intact.

## Editable scene UI (current architecture)

The application shell is authored as ordinary uGUI / TextMesh Pro objects in the Unity scene so its layout and visual design can be edited while the scene is not running.

### Complete UI generation

`Tools > Lighting Scenario` exposes **one authoring command only**:

**Tools > Lighting Scenario > Build Complete UI In Current Scene**

Run this command in Edit Mode. It performs the complete setup in one operation:

1. Reuses the existing `LightingScenarioApp` root, or creates it when it does not exist.
2. Ensures the root has `Canvas`, `CanvasScaler`, `GraphicRaycaster`, `AppTheme`, and `LightingScenarioApp`.
3. Ensures the scene has an `EventSystem` and an appropriate UI input module.
4. Removes stale missing-script entries below the application root.
5. Replaces the generated children below `LightingScenarioApp` with the complete default UI hierarchy.
6. Creates/updates the runtime UI Prefab Assets under `Assets/LightingScenarioTool/Prefabs/UI`.
7. Creates/updates `LightingScenarioUiPrefabCatalog.asset` and assigns it to `LightingScenarioApp`.
8. Creates and attaches the static scene UI, `TimelinePanel`, `PreviewPanel`, and timeline interaction scripts.
9. Assigns the serialized references required by the application and marks the current scene dirty so the generated UI can be saved normally.

Running the command again intentionally regenerates the generated UI under `LightingScenarioApp`; it is not a partial repair command. This keeps setup deterministic and removes the previous distinction between Create / Build / Rebuild menu commands.

The generated static UI is not repaired or regenerated automatically when entering Play Mode. Runtime code validates the authored hierarchy and reports an error if required objects or components are missing. Regenerate the UI explicitly with the command above when the structure must be reset.

### Hierarchy / Prefab ownership

The persistent application shell is scene-authored, while repeated or transient widgets that have their own visual/interaction meaning are Prefab Assets.

```text
LightingScenarioApp
├─ Background
│  ├─ MenuBar
│  ├─ ScenarioHeader
│  ├─ TimelineToolbar
│  ├─ KeyframeEditor
│  └─ Workspace
│     ├─ TimelineArea
│     ├─ PaneDivider
│     └─ PreviewArea
└─ Overlay
```

The complete UI command creates/updates these runtime Prefab Assets under `Assets/LightingScenarioTool/Prefabs/UI`:

- `TimelineTrackLabel.prefab`
- `TimelineTrackTime.prefab`
- `ColorKeyframe.prefab`
- `TimelineRulerInteraction.prefab`
- `TimelineRulerLabel.prefab`
- `TimelinePlayhead.prefab`
- `TimelineRulerPlayhead.prefab`
- `PreviewLight.prefab`
- `RuntimeMenuPopup.prefab`
- `RuntimeMenuItem.prefab`
- `HsvColorPicker.prefab`
- `ColorPresetSwatch.prefab`
- `RuntimeDialog.prefab`

Runtime code instantiates these assets through `LightingScenarioUiPrefabCatalog`; the Scene no longer contains hidden Track/Keyframe/Preview template objects. Edit the Prefab Assets in Prefab Mode when changing the visual design of repeated widgets.

Simple high-count drawing primitives remain procedural by design. Timeline grid lines and ruler tick marks are rendered by dedicated `Graphic` components, and color interpolation is rendered by a dedicated gradient `Graphic`. Row separators are part of the Track Prefabs. This avoids creating large numbers of tiny Prefab instances for elements that are only drawing primitives.

`LightingScenarioApp.Awake()` validates both the static scene hierarchy and the Prefab Catalog. If either is missing, Play Mode reports an explicit setup error instead of constructing a fallback UI.

## Prefab script safety

All MonoBehaviour scripts serialized into Scene objects or generated Prefab Assets live in same-named `.cs` files. The complete UI generation command removes stale missing-script entries from the Scene before rebuilding the hierarchy, and generated Prefabs are recreated from typed components rather than copied from stale Scene templates.


## Current UI controls

- Timeline Toolbar no longer contains Zoom In / Zoom Out buttons. The same commands remain under **View > Zoom In** and **View > Zoom Out**.
- `MenuProjectText` has been removed. Project filename/state remains in the Scenario Header Project section.
- Existing scenes are not migrated implicitly. Run **Build Complete UI In Current Scene** when adopting a newer default UI structure.

## Inspector-editable AppTheme colors

`AppTheme` is now a scene component on the same GameObject as `LightingScenarioApp` instead of a static-only color table.
Select `LightingScenarioApp` in the Hierarchy and edit the `AppTheme` component to change the colors used by dynamically-created UI.

Inspector color groups include:

- Surfaces: Background, Panel, Elevated, Input Background, Preview Canvas, Divider
- Timeline: Track rows/headers, Grid, Playhead
- Text: Primary, Secondary, Disabled
- Accent / Selection: Accent, Hover, Pressed, Tint
- Button State: Disabled
- Input State: Highlighted, Pressed, Selected, Disabled
- Semantic: Error, Warning, Success

The complete UI generation command ensures the `LightingScenarioApp` root has an `AppTheme` component.

The spacing and typography constants remain code constants; this change moves the color system specifically into Inspector-editable serialized fields.
