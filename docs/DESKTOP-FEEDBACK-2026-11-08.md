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

The full desktop logic suite passed: 816 tests, no failures or skips. New tests cover palette grouping and undo branches, duplicate-ID repair and persistence, style conversion with groups/bindings, and changing the selected theme while battery saver remains active.

The Release WinUI build passed with no warnings or errors. The bundled documentation was compared with its source files. Tests used `THEMEDAI_LOCAL_DATA_ROOT` pointing to an isolated folder. Personal widgets were not used as fixtures.

Computer Use failed during initialization with `failed to write kernel assets: The system cannot find the path specified. (os error 3)`, including after resetting the runtime. These results are not GUI acceptance.

## Still open

- The user clarified that missed keystrokes occur in the VibeFinder prompt. General prompt typing, focus and playback command handling need a working desktop/browser reproduction. The hex-field fix is separate.
- Playlists and history still need authenticated endpoint diagnostics. Public deployed JavaScript was inspected, but the route probe was not executed because automatic approval review hit a usage limit. No account data or credentials were used for that inspection.
- Weather still uses OpenWeatherMap. A key-free or centrally managed replacement has not been implemented. Provider terms, caching, location consent and distribution limits must be settled before choosing a replacement.
- The full app-wide plain-language pass and all GUI acceptance checks remain open.
- Existing cross-repository security work remains separate. This branch does not merge, deploy or validate those fixes.

## Legal document review

The supplied publisher identity is used without inventing a state, postal address, company registration or exclusive court venue. The privacy policy discloses the current local credential storage and optional online processing, including Gemini when enabled. It does not promise a completed audit, immediate deletion, a fixed server-retention period or India-only hosting.

Before publishing as final legal documents, review the deployed backend's data inventory, provider agreements, account-deletion process, retention and backup periods, handling of children's data and applicable legal obligations. No production configuration or consent flow was changed by adding these documents.

Official references checked on 8 November 2026:

- [MeitY: notified Digital Personal Data Protection Rules, 2025](https://www.meity.gov.in/static/uploads/2025/11/53450e6e5dc0bfa85ebd78686cadad39.pdf). The notification provides phased commencement. Do not present every rule as already effective.
- [India Code: Consumer Protection Act, 2019](https://www.indiacode.nic.in/bitstream/123456789/15256/5/A2019-35.pdf). The draft terms preserve mandatory rights rather than imposing a blanket exclusion.
