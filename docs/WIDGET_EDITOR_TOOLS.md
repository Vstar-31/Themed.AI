# Widget editor: history and layout tools

The widget editor now includes Undo/Redo, a layer tree, group and meter stacking, duplication, multi-selection alignment,
equal-gap distribution, and keyboard movement. These operate on the editor draft; Save applies it to the running widget.

## Undo and redo

- Use the toolbar buttons, Ctrl+Z, Ctrl+Y, or Ctrl+Shift+Z. Text inputs retain their own text undo shortcuts;
  use the toolbar to undo a draft operation while a text field has focus.
- History includes meter/measure/group structure and properties, visual states, and persisted anchors
  and responsive rules. A preview drag, grouping/ungrouping, alignment, distribution, or stacking
  action is one undo step. Text/property changes are individual edits.
- A new edit after Undo discards the redo branch. No-op changes do not add history.
- History is local to the current editor session, with up to 100 undo steps and an 8 MiB serialized
  history budget. Very large drafts can retain fewer steps. It does not undo widget deletion or changes
  made outside the editor. Preview simulation inputs and selection changes are not draft edits.
- Returning to the saved draft clears the unsaved flag. Undo preserves the current selected meter/group
  when their IDs still exist; otherwise the editor selects the first available item.

## Arrange

Select a meter, then use Arrange to bring it forward/to front or send it backward/to back.
Grouped meters reorder within their group. Ungrouped meters reorder alongside whole groups at the root.
The preview uses the host's ZIndex ordering, including the maximum member ZIndex for a group container,
group insertion ordering on ties, and meter ID tie-breaker. The meter list remains in creation order.

## Layer tree and duplication

The Layers panel lists the widget from front to back, with expandable groups and their meters.
Selecting a row selects its property inspector and preview. Forward/Backward moves the selected layer
one position; More offers Front/Back. Groups move as units, preserving internal order and transforms.
Empty groups cannot be ordered or duplicated because they have no rendered stacking value.

Duplicate copies the selected meter or group, including its properties and visual states. New meter,
group and state IDs keep the copy independent. Group membership is remapped to the new meters, while
measure bindings and image file references continue to reference the same sources. Copied groups get a
unique copy name; copies are offset 16 DIP and placed just in front of the source. A grouped meter copy
stays in its parent, so the group's clipping may hide part of the offset copy. Each duplication/reorder
is one undo step. Moving all members into a new group removes the vacated group containers.

The tree is single-select; use the meter list for multi-select alignment/grouping. Drag-and-drop
reparenting, nested groups, layer lock/visibility, and bulk tree selection are not implemented.

## Align and distribute

Select at least two meters in the list and choose Align. Options include left/right/top/bottom edges
and horizontal/vertical centers, using the bounding rectangle of the selected layout boxes.
Distribution requires at least three meters and places equal gaps within their overall bounds.
Insufficient space produces guidance rather than introducing overlap.

All selected meters must have the same parent group, or all must be ungrouped. Alignment uses local,
unrotated layout boxes; it does not calculate bounds of animated or rotated content. Dragging uses the
parent canvas coordinate system, including when a group is scaled or rotated. Snap-to-grid applies to
dragging; explicit alignment/distribution keeps exact coordinates.

## Keyboard movement

- Select a layer, then use **Ctrl+D** to duplicate it.
- **Ctrl+Alt+arrows** move the selected layer by 1 DIP. Add **Shift** to move by 10 DIP.
- For multiple meters, select them in the meter list. All must share one parent; each key press is
  one undo step. Group movement changes its position without changing child coordinates or transforms.
- Meter movement uses its parent group's local coordinates. A scaled/rotated group therefore changes
  the apparent screen direction/distance. Negative positions are allowed, as in the position fields;
  clipping may hide content. Keyboard movement does not snap to the drag grid.
- Text fields and combo boxes retain their own keyboard behavior. Editor shortcuts require focus
  inside the editor and are suppressed during save, image selection, draft export, and dialogs/popups.
  Ordinary arrows still navigate controls and the layer tree.

## Verification status

As of 2026-10-22, the main suite passes all 748 cases and Windows Release x64 publish succeeds.
Editor logic tests use actual view-model source with explicit dispatcher/persistence doubles. They cover
history, structural edits, ordering, alignment, draft lifecycle, export, keyboard movement and recovery.
They do not exercise WinUI input routing, bindings, rendering, or the real manager integration.

GUI acceptance remains outstanding: keyboard focus/routing (including text fields, dialogs and after
duplication/history), pointer cancellation, transformed-group drags, stacking persistence, and mixed-size
alignment. See [image assets](WIDGET_ASSET_BROWSER.md) and [local template remix](WIDGET_REMIX.md) for
the implemented related workflows. Full keyboard-only authoring is not yet verified.

Static meter rendering is shared with the starter picker. Graph preview meters now show a fixed
sample line and shaded history area scaled by the sample fraction; this is illustrative, not live
data or a simulation of the configured history length. Text, bars, rings and other editor meter
previews retain their existing appearance. Visual rendering still needs desktop acceptance checks.
