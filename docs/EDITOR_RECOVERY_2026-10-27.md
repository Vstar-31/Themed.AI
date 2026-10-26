# Editor recovery and authoring additions

Implemented on 2026-10-27 without launching or controlling desktop applications.

- Unsaved editor work receives a local checkpoint every two seconds while not dragging or saving, plus a final checkpoint when navigating away. Atomic replacement retains the preceding checkpoint if writing fails. Drafts use AppDataPaths.LocalRoot/ThemedAI/Drafts and therefore obey THEMEDAI_LOCAL_DATA_ROOT isolation.
- Widgets > Add widget > Recovery drafts lists checkpoints by latest save time. Recover opens a new disabled remix requiring explicit Save. Discard requires confirmation. Unreadable files remain on disk and are reported; a corrupt file does not hide valid drafts.
- Editor status distinguishes explicit widget saving from local recovery checkpoints and offers Retry checkpoint. Explicit save preserves newer edits and rejects overlapping saves. Successful clean saves remove the checkpoint; cleanup failure keeps the editor open for retry. Explicit Back/discard removes its checkpoint before navigating.
- Align > Snap selection to grid rounds selected meters to the existing 10-DIP grid in one undo transaction. Stale selections, mixed parents, invalid grids and non-finite results are rejected atomically.

Limits: checkpoints are local JSON, not an encrypted backup or a portable package. Referenced images remain local paths. A crash before the next checkpoint can lose the latest edits; in-flight writes are not synchronously drained on forced process termination. Separate application processes are not coordinated. Recovery history/selection is not restored. Saving a recovered copy never overwrites its original widget. World packages and playback changes are not included in this change.

Validation: 783 Release logic tests passed after adding 20 recovery, corruption, failure/retry, save-status and snap regressions. Final WinUI Release compilation passed with zero warnings and zero errors. No GUI acceptance is claimed for these new controls.
