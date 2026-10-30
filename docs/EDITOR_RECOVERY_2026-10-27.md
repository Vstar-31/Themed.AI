# Editor recovery and authoring additions

Implemented on 2026-10-27 without launching or controlling desktop applications.

- Unsaved editor work receives a local checkpoint every two seconds while not dragging or saving, plus a final checkpoint when navigating away. Atomic replacement retains the preceding checkpoint if writing fails. Drafts use AppDataPaths.LocalRoot/ThemedAI/Drafts and therefore obey THEMEDAI_LOCAL_DATA_ROOT isolation.
- Widgets > Add widget > Recovery drafts lists checkpoints by latest save time. Recover opens a new disabled remix requiring explicit Save. Discard requires confirmation. Unreadable files remain on disk and are reported; a corrupt file does not hide valid drafts.
- Editor status distinguishes explicit widget saving from local recovery checkpoints and offers Retry checkpoint. Explicit save preserves newer edits and rejects overlapping saves. Successful clean saves remove the checkpoint; cleanup failure keeps the editor open for retry. Explicit Back/discard removes its checkpoint before navigating.
- Align > Snap selection to grid rounds selected meters to the existing 10-DIP grid in one undo transaction. Stale selections, mixed parents, invalid grids and non-finite results are rejected atomically.

Limits: checkpoints are local JSON, not an encrypted backup or a portable package. Referenced images remain local paths. A crash before the next checkpoint can lose the latest edits; in-flight writes are not synchronously drained on forced process termination. Separate application processes are not coordinated. Recovery history/selection is not restored. Saving a recovered copy never overwrites its original widget. World packages and playback changes are not included in this change.

Validation: 783 Release logic tests passed after adding 20 recovery, corruption, failure/retry, save-status and snap regressions. Final WinUI Release compilation passed with zero warnings and zero errors. No GUI acceptance is claimed for these new controls.

## GUI verification — 2026-10-27

Used a fresh THEMEDAI_LOCAL_DATA_ROOT and separate WEBVIEW2_USER_DATA_FOLDER under test-evidence/2026-10-27-recovery. No personal inventory was used. PR #10 head 77934e9 passed Debug/Release CI with zero annotations before this picker correction.

Verified through Computer Use and independent disk reads:
- Created an unsaved blank widget, named Recovery GUI Oct27, committed the text field and observed the local checkpoint with Enabled=false.
- Interrupted only the isolated process and relaunched with the same profile. Recovery drafts listed the persisted checkpoint.
- Found a picker bug: Recover copy could close without recovering because the initial selection was lost. Fixed by selecting on ListView.Loaded and enabling Recover/Discard only when an item is selected.
- Rebuilt to a separate folder: zero warnings/errors. Confirmed the first draft is visibly selected and Recover copy opens an unsaved remix.
- Injected a write failure using an empty directory at the isolated checkpoint .tmp path. Editing produced the expected failure status while the previous JSON remained readable and unchanged.
- Removed the fixture obstruction and invoked Retry checkpoint. The saved status returned.
- Explicit Save returned to Widgets, persisted the recovered widget as enabled under its new ID, and removed the recovery checkpoint.
- All four personal skins/themes/scenes/active-scene file hashes matched their before-test baselines.

Stopped the completed isolated test process as cleanup; this is not another graceful-exit test. Still pending: GUI discard confirmation/cleanup, snapping selection with undo/redo, and broader malformed/multiple checkpoint UX. Existing logic tests cover snapping and repository persistence failures.

## Snapping GUI and fresh-profile cleanup — 2026-10-27

A separate profile under test-evidence/2026-10-27-snap contained one disabled fixture with meters at (17,14) and (93,48). GUI selection of both rows followed by Align > Snap selection to grid produced one undo entry. The checkpoint recorded (20,10) and (90,50). Undo returned to the saved draft with 0 undo / 1 redo; Redo restored the snap and 1 undo / 0 redo. The original saved fixture was not overwritten.

Opening this saved fixture on a fresh profile exposed an unrelated cleanup defect: File.Delete raised DirectoryNotFoundException before any Drafts directory existed, displaying a false checkpoint failure. DeleteAsync now treats that specific exception as no checkpoint to delete; other IO/access failures remain visible. Added a regression for deletion before the first checkpoint: 784 tests pass.

The Back button displayed the discard confirmation. The user approved discarding the isolated fixture edits. On 2026-10-28 the Computer Use runtime failed before window access (failed to write kernel assets: path not found), including after a kernel reset. The checkpoint remains on disk; final GUI discard/cleanup and fresh-profile verification of the corrected build remain unverified. No discard pass is claimed.

## Review and failure-path regressions — 2026-10-28

Debug and Release CI passed at e7ba43f in PR run 40676044606 and push run 40676036079, with zero annotations. Reviewed checkpoint locking, discard ordering, save cleanup, picker selection, and copy semantics. Added six regression cases: a read-only checkpoint makes deletion fail visibly while preserving the draft, subsequent deletion succeeds after restoring access, and five malformed collection/identity inputs remain intact without hiding a healthy checkpoint. All 790 Release tests pass. The test build reported NU1900 because NuGet vulnerability data was unavailable; this run does not establish current dependency-audit status.

Computer Use was retried, reset, and retried again; it still failed to write kernel assets before accessing any window. No UI input was sent. GUI discard cleanup and fresh-profile verification remain pending, so PR #10 stays draft. These repository tests do not substitute for desktop acceptance.

## Final focused GUI checks passed — 2026-10-28

Computer Use recovered. Launched builds/2026-10-27-cleanup-fixed with THEMEDAI_LOCAL_DATA_ROOT and WEBVIEW2_USER_DATA_FOLDER under test-evidence/2026-10-28-final-gui. This binary contains the production cleanup fix; subsequent commit 934aa3e changes tests and documentation only. The sandbox-launched process had no accessible window and was stopped by exact PID/path; interactive PID 20776 was used for acceptance.

- Opened the disabled Snap GUI fixture on a fresh profile. After the checkpoint timer ran, the editor displayed Saved and All edits saved to the widget. Independently confirmed the Drafts directory did not exist. No false recovery failure appeared.
- Changed only the fixture name and committed the field with Tab. The editor showed Unsaved edits and then Recovery checkpoint saved locally.
- Chose Back and the authorized Discard changes action. The app returned to Widgets with Snap GUI fixture still Off.
- Independent disk verification found zero checkpoint JSON files. The saved fixture retained its original name, Enabled=false, and meter coordinates (17,14) and (93,48).

These complete the focused discard and fresh-profile checks previously blocked by the runtime. No broader playback or desktop acceptance result is implied. All test writes used the isolated profile; personal storage was not selected. No new personal-file hash comparison was taken this pass.
