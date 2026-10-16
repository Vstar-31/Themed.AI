# Windows build and desktop verification

Use `feat/widget-package-workflow`. The baseline `bcf69da` passed Windows CI run
[38899066623](https://github.com/Vstar-31/Themed.AI/actions/runs/38899066623).
Changes after that commit need their own passing run; a successful build does not establish
that playback, WorkerW desktop windows or monitor changes work on a real desktop.

## Build and run

Install the SDK selected by `global.json` (currently 10.0.204), the .NET 8 runtime for tests,
and Visual Studio's Windows application development tooling. Use a Visual Studio version
that supports that SDK if building inside the IDE. Install Microsoft Edge WebView2 Runtime.
Run on Windows 10 build 19041+ or Windows 11, x64.

From PowerShell in your repository:

```powershell
git switch feat/widget-package-workflow
git pull --ff-only origin feat/widget-package-workflow
.\scripts\Test-Windows.ps1 -Configuration Release -Publish
.\artifacts\windows-x64\ThemeManager.WinUI.exe
```

The script stops at the first restore, build, test or publish error. It produces a complete
unpackaged application directory, including .NET and Windows App SDK dependencies. Keep the
directory together; copying just the EXE is insufficient. WebView2 Runtime is separate.
For debugging, open `ThemeManager.sln`, select `ThemeManager.WinUI` as startup project,
choose Debug/x64 and press F5. The project uses `WindowsPackageType=None`.

GitHub Actions runs Debug and Release checks and uploads the Release directory as
`ThemedAI-windows-x64-<commit>`. Download and extract the entire artifact before launching it.
This is a test build, not a signed installer or update mechanism.

## Desktop smoke test

Back up `%LOCALAPPDATA%\ThemedAI` before testing persistence or migration. Record the commit,
Windows build, display arrangement, DPI, and first failing step. Keep existing user data for
the upgrade test, and use a separate Windows account for the clean-install test.

| Scenario | Steps | Expected result |
| --- | --- | --- |
| Startup | Launch twice, closing cleanly between runs | Main window opens; saved widgets appear once; no crash or duplicate windows |
| Studio browsing | Open Studio with “Apply aesthetic on selection” off; select several worlds | Desktop theme and widget positions remain unchanged |
| World memory | Apply A, move widgets, apply B, move widgets, return to A; restart | Each world restores its own positions and visibility |
| Rapid switching | Switch between two multi-widget worlds 20 times | Final selected world wins; no missing or duplicate windows |
| VFAI connection | Open VibeFinder, sign in, run a prompt | Original VFAI UI and filters load from `https://vfaithemed.netlify.app/app` |
| Repeated transport | Play → Pause → Play → Pause → Play, then Next → Pause → Play → Previous | Track and play state stay synchronized between page and widgets |
| Multiple music widgets | Enable three VFAI widgets, play, disable one, navigate away and back | Remaining widgets and playback continue; app stays open |
| Slow refresh | Use a slow test measure with a 50 ms widget interval | At most one refresh/UI update is outstanding per widget; no growing queued backlog |
| Network recovery | Disconnect, reconnect, retry sign-in/analysis; sleep and resume | A recoverable error appears; no crash or stale playback takeover |
| Images/packages | Follow `WIDGET_PACKAGES.md`, including corrupt and oversized packages | Imports stay disabled; images survive export/import and restart; failures are visible |
| Display changes | Test 100%, 150%, 200% DPI; disconnect/reconnect a second monitor | Widgets remain usable and reachable; record any placement regression |
| External actions | Click an HTTPS action, then a test action pointing to `calc.exe` or `file:///C:/Windows/System32/calc.exe` | Browser link opens; executable/file actions are blocked |
| Embedded origin boundary | Navigate the VFAI WebView to an unrelated HTTPS site or a lookalike hostname; send a non-string bridge message | Navigation/message is ignored; no credentials injected and no crash |

External navigation and popups are blocked inside the credentialed VFAI WebView. Use **Open
web version** for external account-linking/OAuth flows. Login on the new site may require a
fresh sign-in because browser storage is scoped to the origin.

## Diagnose failures

- Build/test logs: `artifacts/test-results/<Configuration>/build.binlog` and `tests.trx`.
- App logs: `%LOCALAPPDATA%\ThemeManager.AI\logs\app-*.log`.
- Missing SDK: compare `dotnet --list-sdks` with `global.json`; do not silently retarget the app.
- `XamlCompiler.exe` / “Exec format error” on Linux: run the Windows workflow or script.
- WebView initialization failure: repair Microsoft Edge WebView2 Runtime.
- Login 401: credentials were rejected. Browser CORS errors are a different failure.
- CORS: backend `main` currently allows local development plus one `FRONTEND_URL_PROD` value
  (defaulting to the original VFAI site). A comma-separated value is treated as one origin by
  the current code. Both production origins need explicit support to serve both frontends.
  Render configuration is unverified while the connector requires reconnection. Backend main
  changes require owner approval.

Review logs before sharing them: widget definitions can include account credentials and URLs,
and build binary logs can include local paths and build environment values. Share the relevant
exception and reproduction steps rather than the entire application-data directory.

## Security review scope and remaining work

This batch reviewed the desktop login scripts, WebView origin/message boundaries, widget action
dispatch, package extraction and refresh scheduling. It adds exact HTTPS-origin validation,
an in-script origin check to handle navigation races, JSON serialization of credentials, and
an external action allowlist. Built-in media actions and installed plugin handlers remain explicit
paths; arbitrary files or OS protocol handlers no longer fall through to shell execution.

Package imports already enforce entry/expanded-size limits, reject traversal and symlink entries,
write generated filenames into a staging directory, and start disabled. Exports remove the
known network/plugin credential targets. Custom text, variables and links still need review.

Remaining boundaries: saved VFAI passwords are still in local widget data; moving them into
Windows Credential Manager requires a migration. Installed plugin DLLs execute with the app's
user privileges and are trusted code, not sandboxed. Generic WebEmbed content and all backend
routes/dependencies have not received a complete audit in this batch. No claim of “zero security
flaws” or production certification is made.

The formal Codex Security scan could not start because this session exposes none of its scan
tools. Its desktop scan instructions require an authoritative scan ID before preflight/source
workers and prohibit inventing scan ownership. These are development review notes, not a
completed Codex Security scan. Reconnect Netlify and Render to inspect actual deployment
settings and logs; neither production configuration nor backend main was modified.
