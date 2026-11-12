# Saved-account session renewal

An expired `vf_token` caused the native automatic login script to return immediately.
The SPA's presence-only APP_READY signal then started analysis with that expired token.
A 401 signed the SPA out, despite configured credentials in the desktop. A sessionStorage
attempt flag also survived page reloads and could suppress a later renewal.

The desktop now checks JWT expiry (only as a renewal hint; the backend remains the
authentication authority), renews missing/expired/malformed tokens, and limits automatic
attempts to one per document. Near-expiry tokens get a 30-second margin. Explicit Save &
Apply can retry failures. The old SPA cannot trigger native initial analysis while a login
is in flight; the authenticated reload supplies the next ready notification. Both the
visible page and hidden prewarm browser use this check. No new frontend change is required.

Validation:

- Release WinUI build: zero warnings/errors.
- 833 desktop tests pass, including 31 VibeFinder tests.
- Node executes the actual C#-generated scripts with fake credentials and browser/network
  fixtures: missing/expired/malformed/near-expiry/current tokens; concurrent injections;
  old sessionStorage flag; authenticated reload; rejected credentials; network/server errors;
  explicit retry; untrusted origin; password serialization. No real credentials are in fixtures.
- Native GUI: reopened the corrected build with the existing isolated test data and WebView
  profile. It signed in without credential entry (Sign Out visible), then returned an
  authenticated five-track calm recommendation at 58% for the saved theme.
- Native GUI: `rainy night drive with mellow electronic music` remained intact (46 characters),
  five-track selection worked, and Run Analysis returned **chill, 62%, 70–100 BPM** against the
  newly deployed backend. Previously this became focus at 2% with direct-search results.

The original failed SPA had already removed its expired token before this restart. Expired-token
renewal itself is covered by executable fixtures; the GUI check confirms saved-account reuse
and the full authenticated recommendation path. No passwords or tokens were inspected.
Renewal is checked on navigation/readiness; this does not add a background refresh timer or
change explicit Sign Out behavior. Existing session revocation still belongs to the backend.

Personal widget data was not used. The isolated test profile is under
`test-evidence/2026-11-11-embed/run-105046` in the task workspace.
