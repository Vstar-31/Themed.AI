# Remix widgets and starter templates

On **Widgets**, use **Add widget → Remix a starter…** to search the bundled starter catalog.
The picker shows a scaled visual sample, a short description, size and meter/data-source counts.
Search by name, data source or meter type (for example `clock`, `CPU bar`, `network graph` or `ring`).
Search ignores case and extra whitespace; every word must match a word prefix somewhere in the
starter's name, description, labels, tags or types. A result count and empty-state guidance explain
when nothing matches. Filtering clears selection and disables Open if the selected starter is hidden.
Choose **Open in editor** to
customize an independent draft. To start from an existing widget, choose **Remix** on its card.

Previews use fixed sample data and the current app colors, not live readings. Text, bars, rings and
graphs share the editor's static renderer. Graphs now show an illustrative line/area history rather
than a filled rectangle. The picker scales designs down to fit while preserving proportions and
does not start measures, network requests, actions or hosts. Its current visual previews cover the
five bundled, ungrouped primitive starters; future complex/image/web starters show an editor prompt
instead of loading external content. These samples do not simulate state transitions or placement.

No widget is created or enabled until **Save**. Back/Discard abandons the new draft, and Delete is
disabled until its first save. Save persists a new widget identity before creating its live host.
Existing-widget saves still reject deleted identities instead of silently recreating removed widgets.

**New blank widget** now uses this same unsaved-draft flow. Back/Discard does not leave an empty widget
in the library. Blank names use a unique numbered suffix when needed.

Use **Export draft…** in the editor to package its current snapshot without saving/enabling it. Export
does not clear dirty state or undo history, and it includes image assets through the existing package
service. Cancellation, invalid definitions, missing images and write failures retain the draft; failed
exports do not replace an existing destination. The review prompt explains omitted connection settings
and the need to review custom text/links/variables before sharing.

The source retains its settings. The copy receives fresh widget, meter, measure, group and state IDs;
group membership is remapped to copied meters. Group transforms/variables, state definitions, anchors,
measure names, formulas, local connection settings and image references are retained. Nested model
objects are independently editable. Images remain shared files until replaced through the asset browser.
Names use a case-insensitive `remix`, `remix 2`, … suffix, and copies lose the built-in tag.

Copies start disabled and editable: lock, click-through, desktop-layer and always-on-top flags are off.
Online starter widgets may require connection settings. Saving enables the copy; exporting afterward
uses the existing widget package export flow, including its connection-setting omissions.

If a save is still running while you make additional changes, the earlier snapshot is saved and the
newer edits remain unsaved in the editor. An I/O failure retains the draft. If its original widget was
removed or its identity conflicts, **Keep edits as new widget** appears after the failed save. This
converts the current edits (including edits made after the failure) into an independent unsaved remix.
Choose **Save** again to create it. The conversion resets history to the new draft and uses the same
identity/window-flag rules as remix; it does not overwrite or restore the removed source. This is
in-session recovery, not autosave or crash recovery. Ordinary storage failures retain the original
draft for retry and do not offer identity recovery.

## Verification

The repository's main test project now includes the actual editor view-model source and explicit
dispatcher/persistence doubles under `ThemeManager.Tests/Editor`. All 748 cases pass, including the
keyboard/recovery cases and 19 catalog/search/sample regressions. The existing
Windows test script/CI now discovers these editor regressions without a separate scratch project.
These are logic tests, not WinUI GUI or actual manager persistence integration tests.

Windows Release build/publish verifies the picker and editor wiring compile. The
[22–23 October desktop pass](GUI_ACCEPTANCE_2026-10-23.md) checked bundled previews,
search/selection, scrolling, light/dark contrast, keyboard duplication/movement,
undo/redo, first save, reopen after restart, storage-failure draft retention/retry,
and user-assisted native export/reimport. It found and fixed stale stock-control
colors and the dialog/title-bar light/dark mismatch. A follow-up checked asset
selection, transformed-group dragging and source-loss recovery through the real
manager in a temporary fault-injection build. The report distinguishes that
setup and remaining desktop coverage from automated coverage.
Remote gallery package browsing/remix, richer previews for complex templates and reference-aware asset cleanup
remain separate future work.
