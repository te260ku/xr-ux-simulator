# Lighting Scenario Tool

Runtime uGUI / TextMesh Pro editor for authoring lighting scenarios in Unity.

## Current model

Each Lighting Unit owns one Lighting Track containing Color Keyframes only. There is no clip concept.
Lighting color is linearly interpolated between keyframes; before the first keyframe the output is Black,
and after the last keyframe the final color is held. Muted tracks evaluate to Black.

## UI layout

The runtime UI is organized into five areas:

1. **Menu Bar** - File / View / Help
2. **Project / Scenario Bar** - Scenario name, Duration, current project filename
3. **Timeline Toolbar** - transport, current time, Loop, Snap, Zoom
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
- Keyframe fill uses the keyframe color. Dark/black keyframes receive a light outline. Selected keyframes use a distinct outline.
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

- Preview-specific controls live in the Preview toolbar: **Background Image...** and **Light Size**.
- Right-click empty preview space to add a Lighting Unit.
- Drag a Lighting Unit to reposition it.
- Click empty preview space to clear selection.
- Lighting Unit names are displayed directly above their square.
- Selected units receive an outline, and the matching Timeline track is highlighted.
- Background image path and Light Size are saved in project JSON and restored on load.
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
- An `Exit` button is available at the upper-right of the Menu Bar and uses the same unsaved-changes confirmation as File > Exit.
