# Fatewake

> **Every choice leaves a wake.**

Fatewake is a persistent narrative survival and social strategy game where player decisions create lasting consequences.

## Current Design

- **Genre:** Persistent narrative survival / social strategy
- **Initial platform:** Mobile-first web/PWA
- **Required daily session:** Approximately 3–5 minutes
- **Structure:** One daily episode with optional deeper play
- **Season One:** *The Silence*
- **Core mechanic:** Wake Effects
- **Principle:** The game engine decides what happens; AI decides how it is told.

## Running locally

Install the .NET 10 SDK and start Docker or Podman, then run:

```powershell
dotnet run --project src\Fatewake.AppHost\Fatewake.AppHost.csproj
```

AppHost starts PostgreSQL, RabbitMQ (with its management UI), the API, the background worker, and the Blazor Web app. Its default launch profile explicitly selects Docker, configures the local Aspire dashboard, and overrides an inherited `DOTNET_ASPIRE_CONTAINER_RUNTIME` setting. User secrets retain local resource passwords across restarts of persisted volumes. For Podman, run with `--launch-profile Fatewake.AppHost.Podman`; make sure the selected runtime is installed and running.

The host is a long-running process: after startup, use the printed Aspire dashboard URL to see resource status and service logs. If resources remain waiting, check the host output for container-runtime warnings. To diagnose a silent `dotnet run` during restore/build, run `dotnet build src\Fatewake.AppHost\Fatewake.AppHost.csproj --tl:off --verbosity minimal` first, then run with `--no-build`. When using `--no-launch-profile`, set `ASPIRE_CONTAINER_RUNTIME` explicitly and supply the dashboard/OTLP endpoint settings normally provided by the profile.

The API applies migrations before becoming healthy; Web and Worker wait for it. API and Worker share the repository-local `data\art` filesystem storage, which is ignored by Git. Deployed workers require an explicitly configured `ArtStorage:RootPath` on shared persistent storage.

`ArtGeneration:Provider` defaults to `None`: approved artwork can be reused without generating new images. For new artwork, configure the worker with `OpenAI`, `Local`, or `ComfyUI` and enable the corresponding provider section. Keep API keys outside tracked configuration files. RabbitMQ supplies wakeup hints; PostgreSQL leases and periodic polling remain the durable execution/recovery mechanism.

Database tests use `FATEWAKE_TEST_CONNECTION` and **delete/recreate that database**. Never point it at your development database. `FATEWAKE_TEST_RABBITMQ` enables the broker wakeup integration test.

## Accounts and local login

Open **Sign in** in the Web app, or visit `/register` to create an account with an email address and a 12-128 character password. Local login is always available; it does not require external-provider credentials. Email lookup is case-insensitive. Passwords use ASP.NET Identity's salted, versioned password hasher; five failed attempts lock password login for 15 minutes.

Registration emails a verification link. Open it and re-enter the password chosen at registration to finish signing in; this prevents an unsolicited registration from activating someone else's chosen password. Links expire after 24 hours and can be used once. `/check-email` resends verification messages. Existing local accounts created before email verification was introduced must verify their email before password login.

Signed-in play resumes your account's survivor across browsers. Guest play remains available, but guest progress is kept separate and is not automatically claimed when registering or signing in. `/account` displays your email, whether local login is configured, and sign-out.

The Web server exchanges credentials with `/api/auth/register` or `/api/auth/login`, keeps the API's eight-hour opaque bearer session inside an encrypted HttpOnly cookie, and uses antiforgery-protected forms. Production requires HTTPS and durable ASP.NET Core Data Protection keys; replicas of each service must share that service's key ring and application name. Signing out clears the browser session; an already copied API bearer token remains valid until expiry. Tokens are never stored in browser local storage.

### Google and Microsoft sign-in

Enable a provider and configure its client ID/secret in AppHost user secrets (AppHost forwards these settings to API and Web), or configure both services separately. For example:

```powershell
dotnet user-secrets set "Authentication:Google:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>" --project src\Fatewake.AppHost
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>" --project src\Fatewake.AppHost
```

Microsoft uses the same settings under `Authentication:Microsoft`. `TenantId` defaults to `consumers` (personal Microsoft accounts); use a specific tenant GUID for organization accounts. Multitenant `common`/`organizations` is not supported. Register the Web app's public callback URLs with your provider: `/signin-google` and `/signin-microsoft` (locally, `https://localhost:63279/signin-google` and `https://localhost:63279/signin-microsoft`). Apple is not implemented.

Provider sign-in uses authorization code with PKCE, state/correlation and nonce validation. API independently validates provider token signature, issuer, audience and expiry. Google `email_verified` can prove mailbox ownership; Microsoft email claims cannot, so first sign-in emails a verification link before creating/linking an account. A proven, case-insensitive matching email links to the existing canonical account without replacing its password or survivor. Newly created external accounts are directed to `/set-password` and continue to see a backup-password prompt on `/account` until configured.

### Verification email delivery

SMTP is disabled by default. AppHost saves each message as a uniquely named `.txt` file in the repository's **`emails` folder**; open that file to follow the link. These files contain private verification links and are ignored by Git. Restrict filesystem access and do not serve that folder publicly. Standalone API execution defaults to an `emails` folder beside its executable; use `Email:Directory` to select an absolute path.

To enable real email delivery, configure these settings in AppHost user secrets (or API configuration):

| Setting | Purpose |
|---|---|
| `Email:SmtpEnabled` | `true` to send through SMTP; `false` to write text files |
| `Email:Host`, `Email:Port` | SMTP server and port (default `587`) |
| `Email:EnableSsl` | SMTP STARTTLS (default `true`; port 465 implicit TLS is not supported) |
| `Email:Username`, `Email:Password` | Optional SMTP credentials; keep in secrets |
| `Email:FromAddress` | Sender accepted by your SMTP server |
| `Email:PublicWebUrl` | Public Web origin used for links; AppHost supplies its HTTPS endpoint |
| `Email:Directory` | Optional file-delivery directory override |

SMTP failures are logged and returned visibly; enabled SMTP never silently falls back to disk. A persisted pending verification can be retried using resend after one minute. Password recovery and guest-to-account transfer are not implemented.

## Documentation

### Logging and performance

API, Web, Worker, AppHost, and the asset-validator tool use Serilog behind Microsoft's typed `ILogger<T>` API. Console output includes service/category and trace correlation. Aspire receives OTLP logs, traces, and metrics; standalone hosts enable export when `OTEL_EXPORTER_OTLP_ENDPOINT` is configured. Operation timings cover accounts, sessions, artwork, and durable work without exposing passwords/tokens/prompts.

See [the permanent observability policy](docs/OBSERVABILITY.md) for configuration, safe logging, instrumentation boundaries, and BenchmarkDotNet commands. The benchmark project measures deterministic resolution and appearance fingerprint allocations/latency; production operation metrics are not a substitute for repeatable benchmarks.

- [Game Design Document](docs/GDD.md)
- [World & Lore](docs/WORLD.md)
- [Gameplay](docs/GAMEPLAY.md)
- [Wake System](docs/WAKE-SYSTEM.md)
- [Characters](docs/CHARACTERS.md)
- [AI Design](docs/AI-DESIGN.md)
- [Technical Architecture](docs/ARCHITECTURE.md)
- [Timelines & Convergence](docs/TIMELINES-AND-CONVERGENCE.md)
- [Decision Log](docs/decisions/DECISION-LOG.md)
- [Season One — The Silence](story/SEASON-01-THE-SILENCE.md)
- [Days 1–7](story/DAYS-001-007.md)

These files are the design source of truth for Fatewake.
