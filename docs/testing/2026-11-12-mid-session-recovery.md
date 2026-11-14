# Mid-session renewal and desktop follow-up

Baseline: master `5b8f1f2`, with the isolated data and browser profile at
`test-evidence/2026-11-11-embed/run-105046` in the task workspace.

Observed GUI results:

- Saved-account startup and authenticated recommendations passed again.
- Play All loaded Heartbeats by José González via YouTube; the position advanced
  to 0:10 of 2:42 and the player exposed Pause, Next, Mute, and Volume controls.
- The first test process subsequently disappeared. No matching Windows Application
  crash event was found in the queried five-hour interval; this is not classified
  as a verified app crash or a successful transport test.
- A restarted test window was then captured behind the Windows PIN/lock screen.
  Input stopped. Repeated pause/resume, next track, minimise/restore, world switching,
  and final visual checks remain pending an unlocked desktop.

Code review confirmed that a mid-session analysis 401 called handleLogout, clearing
the prompt despite saved desktop credentials. The module companion change requests
one bounded renewal and retries the exact analysis body once, preserving typed text
and filters. The desktop supports that request only from the existing trusted origin,
with a GUID request ID and configured credentials. The credentials stay native.

The generated script uses the existing fixed HTTPS auth endpoint. It returns no token
through native messages, reloads no page, times out the auth fetch after 15 seconds,
and checks cancellation before writing storage. Sign Out, Reset Engine, unmount, and
manual account replacement cancel the request. Save & Apply also supersedes a pending
renewal. Old desktop versions do not advertise support and get the prompt-preserving
manual reconnect fallback. No backend or standalone frontend branch change is required.

Validation: executable Node fixtures run the actual C#-generated scripts for renewal,
duplicate suppression, rejected credentials/network failure, cancellation, origin checks,
and explicit account replacement. The full Windows suite has 833 passing tests. The
module companion suite has 22 passing tests. Native GUI verification of the new
in-place renewal remains pending. This change handles analysis renewal; it is not a
claim that every library/service request now retries on authentication failure.
