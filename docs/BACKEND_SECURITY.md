# Backend security configuration

The root `main.py` is a backend source snapshot, not part of `ThemeManager.sln`. Its Python
dependency manifest, core modules, optional routers and Prisma schema are not included here.
Changes in this repository do not deploy or verify the separate production backend.

Before running this source, configure `SECRET_KEY` in the backend's secret environment store.
Use a cryptographically random private value with at least 32 bytes; for example, generate one
locally with `python -c "import secrets; print(secrets.token_urlsafe(48))"`. Never commit the
result or place it in frontend settings. All backend replicas and optional routers must use
the same configured value. Missing, blank, short and former built-in default keys fail startup.
Configured key bytes are preserved without trimming or silently generating a replacement.

If the former default was ever used in a deployment, rotate the deployed key and require users
to sign in again. Existing tokens signed with the old key will no longer be valid. Review the
actual backend repository and deployment separately; neither rotation nor deployment is
performed by this local change.

`/api/vibe/story` requires an HS256 token signed with the configured key, an unexpired `exp`
claim and a nonblank string `sub` claim before accessing Gemini or returning its missing-key
fallback. Tokens issued by the existing login flow retain the same format. Invalid tokens
receive HTTP 401; authenticated upstream failures still return `{"story": null}`.

The three Last.fm lookup helpers now send API-key-bearing requests to HTTPS endpoints in both
the aiohttp and urllib transport paths. TLS certificate verification must remain enabled.

Local verification can exercise selected functions with real PyJWT/FastAPI and mocked external
services, but cannot establish full backend startup, optional-router behavior, database
integration or deployed security without the missing application components and configuration.
