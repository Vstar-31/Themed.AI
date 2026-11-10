# Desktop feedback fixes

Based on the user's 7 November desktop test notes. Branch: `codex/desktop-feedback-fixes`, based on `feat/widget-package-workflow` at `cd3a7f4`.

## Implemented

- Avoid stopping an empty native preview player on every browser playback update. This removes the repeated no-op work and log messages; it does not establish that every playback-button failure is fixed.
- Stop adding an existing widget a second time when a legacy world placement is rebound. Check for duplicate placements after resolving their final IDs.
- Repair saved duplicate widget IDs. Identical copies collapse to one definition; different edits remain as inactive recovered copies with new IDs. Close the read handle before saving migrations so Windows can replace the file.
- Group rapid palette edits by control, ending the group on a pause or completed picker interaction. Undo/redo keeps the start and final states. Leave incomplete hex input alone while typing.
- Avoid an expected missing-folder exception when deleting a draft that does not exist.
- Check automation immediately after saving, recognize a new theme assigned to an active rule, and allow a weather condition without an assigned theme to fall through to other rules.
- Add a desktop-placement preview to the right editor panel.
- Add a Change style menu for text, bar, ring and graph elements. Preserve IDs, bindings, actions and group membership; record the change as one undo step.
- Clarify Studio selection switches.
- Add offline privacy, terms and help documents under Settings, identifying Vijay Shankar, India, and themed.aiapp@gmail.com. Replace the blanket offline/telemetry claim with a description of optional online features.

## Verification

The final desktop logic suite passed: 832 tests, no failures or skips. New tests cover palette grouping and undo branches, duplicate-ID repair and persistence, style conversion with groups/bindings, and changing the selected theme while battery saver remains active.

The Release WinUI build passed with no warnings or errors. The bundled documentation was compared with its source files. Tests used `THEMEDAI_LOCAL_DATA_ROOT` pointing to an isolated folder. Personal widgets were not used as fixtures.

Computer Use initially failed with a kernel-assets error, then recovered later in this session. The final isolated build rendered a live key-free weather widget: 27 degrees, Jaipur, Clear sky. The signed-in deployed VibeFinder library initially showed a history load error; after an isolated app restart with the saved test session, playlists and history rendered and all three library GET routes returned HTTP 200. This demonstrates recovery, not a proven cause of the earlier failure. These checks are not full GUI acceptance of the unpublished frontend fixes.

## Still open

- VibeFinder fixes are prepared in the separate `codex/vfai-feedback-fixes` branch: remove duplicate command handlers and DOM polling, preserve typed prompts during reconnect, await video prefetches, retry transient lookup failures, and wire saved-playlist/history actions. They need deployment and an end-to-end typing/playback retest. The hex-field fix is separate.
- Library failures now have status-only native diagnostics. Anonymous route probes returned HTTP 401 as expected; authenticated GUI requests returned HTTP 200 after restart. The prepared frontend handles per-tab errors, timeouts, retries and expired sessions instead of treating failed requests as empty collections.
- Key-free Open-Meteo weather is implemented for the user-confirmed free, non-commercial preview. Settings, widgets and generation defaults accept a city without a key. Legacy keyed configurations are preserved. Provider endpoints and cache intervals are configurable; see `WEATHER-PROVIDER.md`. Wider/commercial distribution needs provider-capacity/licensing review.
- The full app-wide plain-language pass and all GUI acceptance checks remain open.
- Existing cross-repository security work remains separate. This branch does not merge, deploy or validate those fixes.

## Legal document review

The supplied publisher identity is used without inventing a state, postal address, company registration or exclusive court venue. The privacy policy discloses the current local credential storage and optional online processing, including Gemini when enabled. It does not promise a completed audit, immediate deletion, a fixed server-retention period or India-only hosting.

Before publishing as final legal documents, review the deployed backend's data inventory, provider agreements, account-deletion process, retention and backup periods, handling of children's data and applicable legal obligations. No production configuration or consent flow was changed by adding these documents.

Official references checked on 8 November 2026:

- [MeitY: notified Digital Personal Data Protection Rules, 2025](https://www.meity.gov.in/static/uploads/2025/11/53450e6e5dc0bfa85ebd78686cadad39.pdf). The notification provides phased commencement. Do not present every rule as already effective.
- [India Code: Consumer Protection Act, 2019](https://www.indiacode.nic.in/bitstream/123456789/15256/5/A2019-35.pdf). The draft terms preserve mandatory rights rather than imposing a blanket exclusion.

## Follow-up implementation

- Share weather readings and geocoding across widgets and automation. Cache readings for 30 minutes, back off failed attempts, expire readings after two hours, round automatic coordinates, and use invariant numeric formatting.
- The live smoke check exposed that `Jaipur, IN` needed an explicit country filter. The corrected query and live widget both passed.
- Native Save & Apply explicitly requests replacement of a web prompt; ordinary reconnect/theme synchronization does not override a user-edited prompt in the updated frontend.
- Build output used for the final weather GUI check: `artifacts/feedback-final/ThemeManager.WinUI.exe`. Only test data under the workspace's `test-evidence/2026-11-08-feedback-gui/data` was used.
- The frontend branch has 20 passing tests and a successful production build. Focused lint passes for the new hooks, regression tests and simplified profile bridge. The branch builds on the existing, separate endpoint-hardening work; this desktop batch does not deploy that work.
