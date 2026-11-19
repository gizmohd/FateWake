# Pathweaver

> **Every choice shapes reality.**

**From the universe of The Wayfinder's Heir.** There are those who find the path. And those who weave it.

Pathweaver is an episodic science-fiction narrative adventure in **The Wayfinder Universe**, at **thepathweaver.com**, where player decisions shape branching realities.

Players are **Pathweavers**. Daily episodes are **Chapters**, major story arcs are **Acts**, alternate timelines are **Threads**, and timeline convergence events are **Convergences**. The brand's visual direction is **16-bit cinematic pixel art**. This rebrand does not regenerate or post-filter existing artwork; archival masters, high-quality delivery derivatives and the shared runtime art contract remain protected.

Technical project/namespace names, database identifiers, API routes, cookies, existing saves and OAuth client/audience IDs retain `Fatewake`/`fatewake` for compatibility. Configure production hosting, SMTP and sign-in callbacks explicitly; public branding does not redirect local authentication to the marketing domain.

## Current Design

- **Genre:** Episodic science-fiction narrative adventure
- **Initial platform:** Mobile-first web/PWA
- **Required daily session:** Approximately 3–5 minutes
- **Structure:** Daily Chapters with optional deeper play, grouped into Acts
- **Season One:** *The Silence*
- **Core mechanic:** Player-driven branching realities (Threads), with consequential Wake Effects
- **Principle:** The game engine decides what happens; AI decides how it is told.

## Initial local AI models

See [model setup scripts](tools/models/README.md) for dry-run-first Qwen3 14B/8B preparation and opt-in local ComfyUI configuration. These prepare installed runtimes; narrative-model game integration remains planned.

## Running locally

Install the stable .NET 11 SDK (`global.json` selects `11.0.100` or a newer .NET 11 feature band) and start Docker or Podman, then run:

```powershell
dotnet run --project src\Fatewake.AppHost\Fatewake.AppHost.csproj
```

AppHost starts PostgreSQL, RabbitMQ (with its management UI), the API, the background worker, and the Blazor Web app. Its default launch profile explicitly selects Docker, configures the local Aspire dashboard, and overrides an inherited `DOTNET_ASPIRE_CONTAINER_RUNTIME` setting. User secrets retain local resource passwords across restarts of persisted volumes. The PostgreSQL and RabbitMQ containers are persistent: they keep running after AppHost stops and are reused on the next start. Stop or remove them explicitly with `docker stop`/`docker rm` when needed. For Podman, run with `--launch-profile Fatewake.AppHost.Podman`; make sure the selected runtime is installed and running. Visual Studio checks the container runtime named by the user/machine `DOTNET_ASPIRE_CONTAINER_RUNTIME` environment variable before launching, ignoring the launch profile override. If that variable names a runtime that isn't installed, F5 hangs before `Program.cs` runs. Set it to an installed runtime, or remove it, then restart Visual Studio. The AppHost launch profiles set `"hotReloadEnabled": false` because Visual Studio's Hot Reload startup hook can block before `Main` runs. Hot Reload still works for the API, Web, and Worker projects.

The host is a long-running process: after startup, use the printed Aspire dashboard URL to see resource status and service logs. If resources remain waiting, check the host output for container-runtime warnings. To diagnose a silent `dotnet run` during restore/build, run `dotnet build src\Fatewake.AppHost\Fatewake.AppHost.csproj --tl:off --verbosity minimal` first, then run with `--no-build`. When using `--no-launch-profile`, set `ASPIRE_CONTAINER_RUNTIME` explicitly and supply the dashboard/OTLP endpoint settings normally provided by the profile.

The API applies migrations before becoming healthy; Web and Worker wait for it. PostgreSQL stores approved artwork masters and delivery derivatives in `art_binary`, alongside their asset metadata. On startup, the API backfills registered assets from the legacy filesystem configured by `ArtStorage:RootPath`; API and Worker retain that path only as a read fallback while older binaries remain. Keep those source files until backfill succeeds. New artwork writes go to PostgreSQL. Database backups therefore include image binaries and grow with the art library.

The public introduction and current getting-started guide are at `/`; `/play` starts or resumes gameplay. The guide's scene title/day/beat count follow authored story metadata, and it accurately describes character customization as not yet available. Versioned artwork is delivered through the Web app's same-origin `/art/{key}/{version}/{format}` endpoint with ETag validation. Shared approved art is immutable-public; account-owned character art is ownership-checked and privately cached.

`ArtGeneration:Provider` defaults to `None`: approved artwork can be reused without generating new images. For new artwork, configure the worker with `OpenAI`, `Local`, or `ComfyUI` and enable the corresponding provider section. Keep API keys outside tracked configuration files; `src\Fatewake.Worker\appsettings.json` lists every setting with disabled/empty defaults, and the same template appears in `src\Fatewake.AppHost\appsettings.json`. Under AppHost, set them as AppHost user secrets. The same pattern applies to every secret-bearing section (`ArtGeneration`, `ArtPromptAuthoring`, `Authentication`, `Email`): tracked `appsettings.json` holds safe disabled/empty defaults, and AppHost forwards each non-empty user-secret value to the consuming services with `ApiKey`/`ClientSecret`/`Password`-style keys masked as secret parameters:

```powershell
dotnet user-secrets set "ArtGeneration:Provider" "OpenAI" --project src\Fatewake.AppHost
dotnet user-secrets set "ArtGeneration:OpenAI:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "ArtGeneration:OpenAI:ApiKey" "<api-key>" --project src\Fatewake.AppHost
```

Missing valid scene-layer prompts can be authored from canonical server context through OpenAI Responses. The text authoring feature is independently disabled by default; enabling it does not enable image generation. Configure it separately:

```powershell
dotnet user-secrets set "ArtPromptAuthoring:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "ArtPromptAuthoring:ApiKey" "<api-key>" --project src\Fatewake.AppHost
dotnet user-secrets set "ArtPromptAuthoring:Model" "gpt-6-luna" --project src\Fatewake.AppHost
```

The configurable default model is `gpt-6-luna` with reasoning effort `none`, strict structured output, and no tools. The worker saves a validated immutable SQL prompt revision before creating an image job. The initial authored Day 1 prompt set is imported once from `art\prompt-seeds\day1-v1.json`; operational prompts come from PostgreSQL, never prompt Markdown files or hard-coded subject fallbacks. `art\STYLE-BIBLE.md` remains the shared versioned style contract; canonical character/location sheets remain continuity sources.


Semantic RAG retrieval uses pgvector. The worker embeds every chunk without a vector (imports and generated story) with OpenAI embeddings (`text-embedding-3-small`, 1024 dimensions, matching `rag_chunk."Embedding"`). Embedding is off until configured:

```powershell
dotnet user-secrets set "Embedding:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "Embedding:ApiKey" "<api-key>" --project src\Fatewake.AppHost
```

Canon claim extraction runs in the Worker independently of embeddings. To enable it, configure an OpenAI project key with access to the Responses API, then restart AppHost:

```powershell
dotnet user-secrets set "ClaimExtraction:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "ClaimExtraction:ApiKey" "<api-key>" --project src\Fatewake.AppHost
```

The worker processes eligible existing and new imports. In **Admin → RAG corpus → Canon claim review**, admins can approve or reject a fact alongside its supporting quote. **Source auto-approval** lets admins set a minimum confidence per authorized source. This also approves matching pending claims from that source. Clearing the threshold returns future claims to manual review. Existing approvals remain in effect until reviewed again. Approved facts are available to the narrator subject to scope and audience limits.

With scene authoring enabled, new games start with an AI-authored opening at the survivor’s assigned location and time. The narrator uses approved RAG passages and claims when available and follows the world rules to create a local encounter with a decision. The reviewed opening also becomes private Thread canon for continuity. Existing games keep their saved opening. The worker queue is `story.scene`. The server enforces that no more than four consecutive scenes omit a decision. Configure scene authoring on the AppHost:

```powershell
dotnet user-secrets set "SceneAuthoring:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "SceneAuthoring:ApiKey" "<api-key>" --project src\Fatewake.AppHost
```

Signed-in players can opt into **Chat** from the game menu. Nearby conversations use the current realm, room and story minute. The world engine can give cells in the same room a shared `RoomKey` and assign a working network to trusted visits. Friends can message remotely while both visits support the same network. The Silence starts with communications unavailable.

For an OpenAI-compatible image server, use `Provider=Local` with `ArtGeneration:Local:Enabled`, `BaseUrl`, optional `ApiKey`, and `Model`. Restart AppHost after changing secrets. RabbitMQ supplies wakeup hints; PostgreSQL leases and periodic polling remain the durable execution/recovery mechanism.

Day 1 scenes now resolve their authored layers through `POST /api/art/scene/resolve`. Approved artwork is reused; a valid SQL prompt miss creates a coalesced durable authoring job followed by image work when enabled. The renderer polls pending work every five seconds and displays approved PNG/WebP derivatives through the web app's same-origin `/art/{key}/{version}/{format}` route. Disabled providers and terminal job failures are visible in the scene. Scene requests accept only shipped layer keys, never client-supplied prompts.

When running through AppHost, configure `ArtGeneration:*` and `ArtPromptAuthoring:*` in AppHost user-secrets or environment variables; AppHost forwards each section to the consuming services. When running services separately, provide the same provider configuration and PostgreSQL connection to API and Worker, and keep `ArtStorage:RootPath` configured wherever legacy reads are still needed. The seed JSON, shared style, and character/location context ship with API build/publish output; prompt Markdown is not a runtime dependency. `ArtGeneration:CatalogRoot` can override the catalog directory. For ComfyUI setup, see [the local generation guide](docs/COMFYUI-LOCAL-SETUP.md). For portrait output, configure the selected provider's size or ComfyUI workflow accordingly; reusable overlays require alpha-capable workflows/models.

ComfyUI generation stays disabled until the configured workflow file exists, avoiding failed jobs during initial setup. When running API and Worker separately, make that workflow available at the configured path in both services. After exporting the workflow and starting ComfyUI, reload the scene to request its missing artwork.

The PostgreSQL artwork tests use `FATEWAKE_ART_TEST_CONNECTION`, which must point to a disposable database. They cover concurrent SQL prompt misses, admin save-only and selected regeneration, plus the image pipeline with a deterministic fake generator. The text-authoring tests use a fake HTTP provider; no paid or live AI calls are made.

Database tests use `FATEWAKE_TEST_CONNECTION` and **delete/recreate that database**. Never point it at your development database. `FATEWAKE_TEST_RABBITMQ` enables the broker wakeup integration test.

### AppHost-managed local ComfyUI

Install ComfyUI and a compatible Python/GPU runtime first. `ComfyUIHost` is disabled by default. Configure local paths once in AppHost user secrets:

```powershell
dotnet user-secrets set "ComfyUIHost:Enabled" "true" --project src\Fatewake.AppHost
dotnet user-secrets set "ComfyUIHost:PythonExecutable" "<absolute path to python executable>" --project src\Fatewake.AppHost
dotnet user-secrets set "ComfyUIHost:WorkingDirectory" "<absolute directory containing ComfyUI main.py>" --project src\Fatewake.AppHost
dotnet user-secrets set "ComfyUIHost:Port" "8188" --project src\Fatewake.AppHost
dotnet user-secrets set "ComfyUIHost:Arguments:0" "--windows-standalone-build" --project src\Fatewake.AppHost
```

Omit the Windows argument on Linux; add required GPU arguments as indexed `ComfyUIHost:Arguments` entries. Do not supply `--listen` or `--port`: AppHost binds ComfyUI to loopback and owns its port. Stop any manually started instance first; an occupied port fails configuration explicitly. The resource appears as `comfyui` in Aspire, with console logs and health checks. API and Worker remain independent of GPU readiness so gameplay and configured OpenAI fallback remain available. Configure `ArtGeneration:ComfyUI:WorkflowPath`, `OverlayWorkflowPath`, and `Model` for models actually installed locally. Local hosting overrides the remote base URL, clears cloud credentials for local requests, and uses `history`. Disable `ComfyUIHost:Enabled` to return to an externally managed renderer.

For downloaded Z-Image Turbo components in the repository-root `models` directory, use the [shared model paths and Z-Image workflow setup](workflows/README.md#z-image-turbo-with-shared-model-storage). The new background/alpha-overlay templates use separate diffusion, text-encoder, and VAE loaders; they are not interchangeable with an SSD-1B checkpoint filename. Weights remain ignored, and changing the active workflow/model is an explicit opt-in after local generation checks.

## Accounts and local login

Open **Sign in** in the Web app, or visit `/register` to create an account with an email address and a 12-128 character password. Local login is always available; it does not require external-provider credentials. Email lookup is case-insensitive. Passwords use ASP.NET Identity's salted, versioned password hasher; five failed attempts lock password login for 15 minutes.

Registration emails a verification link. Open it and re-enter the password chosen at registration to finish signing in; this prevents an unsolicited registration from activating someone else's chosen password. Links expire after 24 hours and can be used once. `/check-email` resends verification messages. Existing local accounts created before email verification was introduced must verify their email before password login.

Signed-in play resumes your account's survivor across browsers. Guest play remains available, but guest progress is kept separate and is not automatically claimed when registering or signing in. `/account` displays your email, whether local login is configured, and sign-out.

New registration requires first name, last name, city, and a free-text state/region, with each field trimmed and limited to 100 characters. Local registrations retain these details through email verification/resends; newly created Google/Microsoft accounts complete them after proving ownership, before the backup-password prompt. Existing accounts remain valid without retroactive profile collection. Details are private account data, not survivor identity or game-world location, and are not added to tokens or telemetry. The authenticated `/api/auth/profile` update uses the caller's canonical account, not a supplied account ID.

The Web server submits registration to `/api/auth/register`. After an immediate registration, sign-in, or successful email proof, it exchanges an OpenIddict authorization code with S256 PKCE entirely on the back channel. The API access token is kept in an encrypted HttpOnly cookie and expires after 30 minutes. Browser forms are antiforgery-protected. Production requires HTTPS and durable ASP.NET Core Data Protection keys; Web replicas must share a key ring and application name. Signing out clears the browser session; an already copied API access token remains valid until expiry. Tokens are never stored in browser local storage.

`OpenIddictWeb:ClientId` and `OpenIddictWeb:RedirectUri` must match in Web and API. The redirect URI has no query or fragment and identifies the callback; the Web server intercepts the response rather than navigating to it. AppHost forwards both settings. To override them locally:

```powershell
dotnet user-secrets set "OpenIddictWeb:ClientId" "fatewake-web" --project src\Fatewake.AppHost
dotnet user-secrets set "OpenIddictWeb:RedirectUri" "https://localhost:63279/signin-oidc" --project src\Fatewake.AppHost
```

Development uses OpenIddict development signing/encryption certificates persisted for the current OS user, so restarting the API does not invalidate every existing token. Tokens issued by the earlier temporary-key implementation require one new sign-in. All authenticated Web-to-API application calls forward the stored OAuth access token; a signed-in session missing that token fails explicitly rather than becoming a guest request.

Outside Development, supply separate RSA private PEM keys of at least 2048 bits via the deployment secret store and share them between API replicas. For a local non-Development launch, set the AppHost user secrets without putting the keys in source:

```powershell
dotnet user-secrets set "OpenIddict:SigningKey" (Get-Content $signingPemPath -Raw) --project src\Fatewake.AppHost
dotnet user-secrets set "OpenIddict:EncryptionKey" (Get-Content $encryptionPemPath -Raw) --project src\Fatewake.AppHost
```

### SQL Data Protection key ring

Web and API persist ASP.NET Core Data Protection keys in PostgreSQL's `data_protection_keys` table. The API applies the additive migration before readiness; AppHost gives Web the same database connection and starts it after API readiness. Standalone Web deployments also need `ConnectionStrings:fatewake` and must wait for the migration. Web replicas share the stable application name `Fatewake.Web`; API replicas use `Fatewake.Api`. Redis cache eviction no longer affects these keys. Database backups must include this table: deleting keys makes previously protected cookies and antiforgery proofs unreadable.

Development permits unencrypted key XML in the private development database, with the framework's warning. Outside Development, install the same RSA wrapping certificate and private key in each Web/API host's certificate store and configure its thumbprint:

```powershell
dotnet user-secrets set "DataProtection:CertificateThumbprint" "<wrapping-certificate-thumbprint>" --project src\Fatewake.AppHost
```

AppHost forwards the whole `DataProtection` section to Web and API. Its tracked defaults contain no private key. Use a deployment secret/certificate store in production; keep database access restricted and backups encrypted. Retain old wrapping certificates during rotation so historical key entries remain decryptable. This setting protects the Data Protection ring at rest; it does not replace OpenIddict signing/encryption credentials. Switching from filesystem keys to this new SQL ring requires one fresh sign-in.

**Identity administration:** `/admin/users` manages invitations, email changes, roles, status, and administrator-initiated password resets. Deactivation revokes access without deleting survivor history. Players can also request a reset from **Forgot your password?** on the sign-in page. Invitations and password resets are single-use and expire after one hour. They use the configured email delivery service.

`/admin/applications` registers public and confidential OpenIddict clients. Use authorization code with S256 PKCE, an exact registered callback, and the `fatewake-api` scope; clients enabled for refresh may also request `offline_access`. Confidential secrets are shown only on creation or rotation. On the seeded Fatewake Web client, only callback URLs are editable, and the configured redirect and post-logout URLs must stay registered.

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

- [Retro Presentation and Local Narrative AI Requirements](docs/RETRO-AI-REQUIREMENTS.md) — selected visual direction, training/RAG, ComfyUI, and acceptance gates; requirements, not implemented status.

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

Administrator artwork reviews: the first account to complete a successful verified sign-in receives the persisted Admin role; all later accounts receive User. Registration alone does not grant Admin. Assignment is atomic across API processes. Admins can critique scene compositions and versioned layers and request replacements.
