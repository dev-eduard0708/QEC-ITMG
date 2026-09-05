# Google OAuth — local Development (personal Gmail)

Related: [../04-security/AUTHENTICATION.md](../04-security/AUTHENTICATION.md) · [../12-decisions/ADR-0010-authentication.md](../12-decisions/ADR-0010-authentication.md)

Tracked `appsettings.Development.json` keeps `Enabled=false` so the Host starts for Development quick-login without Google credentials.

To turn on real Google Sign-In locally, set `Authentication:Oidc:Enabled=true` plus ClientId/ClientSecret in the gitignored `appsettings.Development.local.json` (see steps below). Empty `AllowedDomains` accepts any verified Google account — Development/testing only.


## Topology

| Surface | URL |
| --- | --- |
| React (Vite) | http://localhost:5173 |
| API (ASP.NET) | http://localhost:5080 |
| OIDC callback (browser) | http://localhost:5173/signin-oidc |

Vite proxies `/auth`, `/signin-oidc`, `/signout-callback-oidc`, `/api`, and `/health` to the API while preserving the browser host for OIDC so ASP.NET builds:

`redirect_uri=http://localhost:5173/signin-oidc`

Do **not** register `http://localhost:5080/signin-oidc` for this Vite Development flow.

## Google Cloud Console steps

1. Create or select a Google Cloud project.
2. Open **APIs & Services → OAuth consent screen**.
3. User type / Audience: **External**.
4. Keep the app in **Testing** mode for local Development.
5. Under **Test users**, add your personal Gmail address (for example `you@gmail.com`).
6. Open **Credentials → Create credentials → OAuth client ID**.
7. Application type: **Web application**.
8. Name: `QEC ITMG Local Development`.
9. Authorized redirect URIs — add exactly:

   `http://localhost:5173/signin-oidc`

10. Create the client and copy **Client ID** and **Client Secret**.
11. Put them **only** in the gitignored file:

    `src/Qec.Itmg.Host/appsettings.Development.local.json`

    Start from the tracked example:

    `src/Qec.Itmg.Host/appsettings.Development.local.example.json`

    Example shape:

    ```json
    {
      "Authentication": {
        "Oidc": {
          "Enabled": true,
          "Authority": "https://accounts.google.com",
          "ClientId": "PASTE_GOOGLE_CLIENT_ID_HERE",
          "ClientSecret": "PASTE_GOOGLE_CLIENT_SECRET_HERE",
          "CallbackPath": "/signin-oidc",
          "SignedOutCallbackPath": "/signout-callback-oidc",
          "AllowedDomains": [],
          "DevelopmentAutoProvisionEmployee": true
        }
      }
    }
    ```

    Empty `AllowedDomains` means any **verified** Google account may sign in. That is acceptable **only** for local Development.

12. Run:

    `.\dev.ps1`

13. Open:

    http://localhost:5173/login

14. Click **Sign in with Google**.

Development quick-login (**Admin** / **Employee**) remains available alongside Google and is Development-only.

## After first personal Google login

- Verified Google OIDC identity is mapped (`sub`, email, name). Google groups/roles never grant ITMG permissions.
- SQL RBAC remains authoritative.
- Existing JIT provisioning creates/links an **Active Employee** (Employee role only) when `DevelopmentAutoProvisionEmployee` is true (Development only). No Admin/IT auto-grant.
- Default landing uses the normal workspace redirect (Employee → `/employee` unless that user already has IT RBAC).

Safe `returnUrl` examples:

- `/auth/login?returnUrl=/employee`
- `/auth/login?returnUrl=/employee/policies/<id>`

Rejected: absolute URLs, `//…`, `/\…`.

## Troubleshooting

| Symptom | Check |
| --- | --- |
| Host fails at startup mentioning missing ClientId/ClientSecret | Create/fill `appsettings.Development.local.json`. Never commit ClientSecret. |
| Google error `redirect_uri_mismatch` | Redirect URI must be exactly `http://localhost:5173/signin-oidc` (not `:5080`). |
| Browser ends on `:5080` | Use the Vite UI at `:5173`; OIDC callbacks are proxied from `:5173`. |
| Access blocked / app not verified | Consent screen Testing + Gmail listed as Test user. |
| Want quick-login only (no Google) | Set `"Authentication": { "Oidc": { "Enabled": false } }` in the local override file. |

## Production (later)

Do **not** leave unrestricted domains in production.

When QEC Google Workspace is available:

```text
Authentication__Oidc__Enabled=true
Authentication__Oidc__AllowedDomains__0=qehc.edu.sa
Authentication__Oidc__ClientId=...
Authentication__Oidc__ClientSecret=...   (secret store / environment only)
```

Production ClientSecret must never live in the repository.
