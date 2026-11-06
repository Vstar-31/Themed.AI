# Security Policy

## System and Scope
Themed.AI is a Windows desktop widget editor and runtime with WebView2 integration to VibeFinderAI. Review native web bridges, credential storage and injection, widget and world imports, plugin loading, widget network requests, external actions and the included Python backend snapshot. The standalone backend's absent optional modules and production deployment require separate review; source changes here do not deploy VibeFinderAI.

## Threat Model and Trust Boundaries
Remote pages, provider responses, imported packages and widget definitions are untrusted. Desktop credentials and local files have the user's authority. Installed in-process plugins have native process privileges; do not claim they are sandboxed. Do not treat a web page as a native trusted caller merely because it can send a message.

## Security Invariants
- Native credentials and privileged messages are limited to the exact configured HTTPS VibeFinderAI origin, with origin checks at the native consumer as well as the script boundary.
- Navigation, popups, redirects and outgoing credentials preserve that trusted-origin boundary.
- Message payloads, widget expressions, formatting, downloads and packages have explicit type, size and resource limits before expensive operations.
- Imports cannot escape their destination or silently execute code, apply Windows settings or change user state beyond the requested operation.
- Credentials remain outside logs, exported packages, source control and public responses.
- Backend private routes verify identity and ownership; administrative routes require separate authorization. Missing keys or optional security modules fail closed.
- Public provider proxies have an explicit allowed purpose and enforce access and resource limits. HTTPS certificate verification remains enabled.
- Saved desktop state is isolated during tests; personal widgets and provider accounts must not be used as security-test fixtures.

## Reportable Findings and Severity Context
Report source-backed boundary violations with a realistic attacker and concrete impact. State the required import, activation, plugin installation or backend deployment prerequisites. Do not imply that local user-authorized capabilities are remotely reachable without evidence.

## Out of Scope, Exclusions, and Accepted Risk
No vulnerability classes or known security failures are accepted or suppressed. Trusted plugin installation is a privileged workflow, not an assurance that arbitrary plugins are safe.

## Known Limitations and Compensating Controls
The included Python backend is a source snapshot with missing dependencies and optional modules. Assess it honestly and review the separate VibeFinderAI repository for its complete backend. Local tests do not establish deployed fixes, credential rotation or provider configuration.
