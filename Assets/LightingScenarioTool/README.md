# Lighting Scenario Tool

Runtime uGUI / TextMesh Pro editor for authoring lighting scenarios in Unity.

## Current timeline model

Each Lighting Unit owns one Lighting Track. A track contains **Color Keyframes only**.
There are no Static clips, Blink/effect clips, clip colors, or Fade In / Fade Out parameters.
The light color is evaluated by linear interpolation between Color Keyframes. Before the first
keyframe the light is Black; after the last keyframe the last color is held. Muted tracks are Black.

## Color Keyframe editing

- Double-click an empty point on a track to create a Color Keyframe.
- Click a diamond to select it.
- Ctrl-click, or enable `Multi`, to add/remove keyframes from a multi-selection.
- Drag a selected diamond to move all selected keyframes together while preserving their spacing.
- Selected keyframes can be deleted, copied, pasted and duplicated together.
- With one selected keyframe, `KF Time(s)` edits its exact time.
- `KF RGB` and `KF HSV` edit the selected keyframe color. With multiple selected keyframes,
  a color change is applied to the whole selection.
- Track Lock prevents keyframe edits. Track Mute affects preview only.

## Preview

- Right-click empty preview space to create a Lighting Unit.
- Drag a Lighting Unit to reposition it.
- Click empty preview space to clear the selection.
- `BG Image` opens a file picker for PNG/JPG/JPEG background images.
- The selected background image path is saved in the project JSON and restored when that project is loaded.
- The background uses aspect-preserving `contain` behavior so the entire image remains visible.

## Project / application controls

- `Browse`: choose a JSON scenario project.
- `Save` / `Load`: save and restore the editor data.
- `Exit`: quit the Windows application. In Unity Editor it stops Play Mode.
- Unsaved changes are confirmed before New or Exit.

## Timeline controls

- Horizontal scrolling is performed only with the thin scrollbar at the bottom.
- Mouse wheel scrolls tracks vertically while the ruler remains fixed.
- Ctrl + mouse wheel zooms the time axis.
- Track names remain fixed on the left while horizontally scrolling.

## Shortcuts

- `Ctrl + Z`: Undo
- `Ctrl + Shift + Z`: Redo
- `Ctrl + C`: Copy selected Color Keyframes
- `Ctrl + V`: Paste Color Keyframes at the current Playhead time
- `Ctrl + D`: Duplicate selected Color Keyframes
- `Delete`: Delete selected Color Keyframes, or the selected Lighting Unit when no keyframe is selected
- `Home`: Jump to start
- `End`: Jump to end
- `Space`: Play / Pause
- `Shift + Space`: Stop
- `Esc`: Stop (or close an open popup)

## Data compatibility

The current data format version is `3.0.0`. Track data stores only Color Keyframes.
Unknown fields from older JSON files are ignored when loading; they are not written back by the current model.

## Timeline navigation

- Mouse wheel: vertical track scroll
- Ctrl + mouse wheel: zoom time axis
- Middle mouse button + horizontal drag: horizontal timeline pan
- Horizontal scrollbar: horizontal timeline scroll
