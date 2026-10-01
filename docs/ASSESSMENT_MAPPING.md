# How this project meets the assessment areas

Where to find each requirement in the code. File paths are relative to the project folder.

## Programming skills

| Level | Evidence |
|---|---|
| **Basic**: control structures, data types, loops, functions, simple logic | `Security/PasswordService.cs` (policy checks, loops, random generation), `Helpers/CsvHelper.cs` (parsing character by character), `Services/LeadService.cs` (`NormalizeTags`, `SetStage`), `Helpers/UiHelpers.cs` (formatting helpers). |
| **Intermediate**: modular code, classes, error handling, intermediate algorithms | Layered structure: `Controllers/` → `Services/` → `Data/` + `Domain/`, with `Models/` for inputs and DTOs. Services are injected (`Program.cs`). Errors are returned as values through `Services/Result.cs` (NotFound / Forbidden / Invalid / Conflict) and mapped to pages or HTTP codes in `Controllers/AppController.cs` and `Controllers/Api/ApiControllers.cs`. Algorithms: least-busy assignment (`LeadService.LeastLoadedRepAsync`), CSV import with per-row validation, paging and sorting. |
| **Advanced**: complex algorithms, design patterns, optimisation, advanced features | See the table below. |

### Design patterns and optimisation

| Pattern / technique | Where |
|---|---|
| Dependency injection | `Program.cs` registers every service; nothing creates its own dependencies. |
| Service layer + DTOs (records) | `Services/*.cs`, `Models/Dtos.cs` |
| Result pattern (no exceptions for expected failures) | `Services/Result.cs` |
| Strategy (selectable assignment rule) | `FormIngestService.PickAssigneeAsync`: round robin, least busy or a named person, chosen per form connection (`AssignmentMode`). |
| Producer / consumer with a background worker | `ICampaignQueue` (channel) + `CampaignSenderWorker`; `ReminderWorker` for due follow-ups and tasks. |
| Cache with explicit invalidation | `Security/UserStateCache.cs` re-validates every session cheaply and is invalidated the moment a role, status or password changes. |
| Query scoping (row-level security) | `LeadService.Scoped()`: every lead lookup goes through one filter, so a rep can only reach their own leads. |
| Unit of work / transaction | `UserService.DeleteAsync` hands over leads and tasks and removes the user in a single `SaveChanges`. |
| Optimisation | `AsNoTracking` on reads, projection straight into DTOs, server-side paging with capped page size, indexes on filtered columns (`Domain/Entities.cs`), foreign-key indexes, and bounded batches in workers. |
| Advanced features | Webhook form ingestion (`FormPayloadParser`, `FormIntegrationServices`), drag-and-drop pipeline over AJAX, campaign tracking and unsubscribe links, calendar sharing, notifications. |

## Database integration

* EF Core code-first model in `Domain/Entities.cs`, context in `Data/AppDbContext.cs`. SQLite by default, SQL Server by configuration (`Database:Provider`).
* Relationships: user ⇄ leads, company ⇄ leads, event ⇄ leads, lead ⇄ notes/activities, composite key for lead stars and calendar shares, tasks linked to leads and users.
* Integrity: unique indexes (user email, lead email, form tokens), restricted deletes so a user, company or event can never silently remove other records, set-null where history must survive.
* Queries: filtered, sorted and paged LINQ queries in the services; aggregates for dashboard and pipeline totals.
* Seeding: `Data/DbSeeder.cs` and `Data/DemoDataSeeder.cs`.

## APIs

* Versioned JSON API under `/api/v1` (`Controllers/Api/ApiControllers.cs`) with bearer tokens (`Security/BearerTokenAuth.cs`).
* Input is bound to typed models with validation attributes (`Models/Inputs.cs`); validation failures return a structured JSON error; failures map to 400, 401, 403, 404, 409 and 429.
* External services: SMTP email (`Services/CoreServices.cs`) and an inbound webhook for Tally, Google Forms or any sender (`Controllers/FormWebhookController.cs`).
* Try it: `tests/api.http` (VS Code REST client) or `tests/api-smoke-test.ps1`.

## Security

| Concern | Implementation |
|---|---|
| Authentication | Cookie sessions for the web UI, bearer tokens for the API, forced password change for new or reset accounts, lockout after 5 failed sign-ins, rate limiting. |
| Password storage | PBKDF2 via ASP.NET Core Identity hasher, password policy, common-password block, timing-safe handling of unknown accounts (`Security/PasswordService.cs`). |
| Authorisation | Policies `AdminOnly` and `CanWrite` (`Program.cs`), role checks inside every service, and per-record ownership: reps only see and change leads assigned to them. |
| Input validation | Validation attributes on every input model, server-side URL and email checks, parameterised queries through EF Core, Razor output encoding. |
| Request protection | Antiforgery tokens on every form and AJAX call, security headers (`Security/SecurityHeadersMiddleware.cs`), `HttpOnly`, `SameSite`, `Secure` cookies. |
| Session control | Security stamp: password change, role change or deactivation ends all sessions and tokens immediately. |
| Accountability | Audit log of sign-ins, user changes, lead changes, exports and deletions (`Services/CoreServices.cs`, admin screen under Users, API at `/api/v1/audit`). |

## Data flow and logic

```
Browser (Razor view / AJAX)  or  API client
        │  HTTP request + antiforgery token / bearer token
        ▼
Controller      binds and validates input, chooses the response
        ▼
Service         business rules, permissions, ownership scoping, audit + notifications
        ▼
AppDbContext    EF Core → SQLite / SQL Server
        ▼
DTO / view-model → Razor view (HTML) or JSON
```

Worked example, "rep opens a lead": `LeadsController.Details` → `LeadService.GetAsync` → `Scoped()` adds `AssignedToId == me` for non-admins → lead mapped to `LeadDetail` → `Views/Leads/Details.cshtml`. Another rep's lead returns "not found".

## Automated tests

`tests/UnitTests` (xUnit, in-memory SQLite built from the real model with foreign keys on):

* `LeadVisibilityTests`: admins see everything, reps only their own leads, staff none; hidden leads cannot be opened, edited, moved, noted, exported or deleted; reps cannot unassign; duplicate emails refused.
* `UserManagementTests`: create, duplicate email, weak password, update guards, delete with and without leads, transfer of leads and tasks, personal data removed, cannot delete yourself or the last admin.
* `SecurityAndAuditTests`: password policy and hashing, `Result` behaviour, audit log storage and filtering.

Run them with `.\build-and-test.ps1` or `dotnet test tests/UnitTests/UncoveringGreatnessCRM.Tests.csproj`.
`tests/api-smoke-test.ps1` exercises the running API end to end.

## Known limitations (be upfront about these)

* The database is created with `EnsureCreated`; for real schema migrations use `dotnet ef migrations add Initial` and set `Database:UseMigrations` to `true` (see README).
* Demo passwords in `appsettings.json` are for testing only and must be changed before going live.

## Front end (9.4.1)

| Area | Evidence |
|---|---|
| Look and feel, consistency | One stylesheet (`wwwroot/css/styles.css`) driven by design tokens in `:root`; shared layouts (`Views/Shared/_Layout.cshtml`, `_AuthLayout.cshtml`) and partials (`_Form`, `_Pager`, `_Avatar`) keep every screen on the same patterns. |
| Branding | Logo (`wwwroot/Images/logo.png`), olive brand palette, Sora (headings), Inter (body) and IBM Plex Mono (figures) used on every screen. |
| Usability and navigation | Grouped sidebar (Workspace / Admin), breadcrumb title in the top bar, global search, pipeline board with drag-and-drop, filters that auto-submit, paging, confirm dialogs on destructive actions. |
| Affordance | Buttons use `.btn` variants with hover/disabled states, clickable table rows show a pointer and keyboard focus, links and menus have visible focus rings. |
| Responsive design | Breakpoints at 1180, 1024, 768, 640 and 480 px: grids collapse, sidebar becomes an off-canvas menu, tables scroll inside `.table-wrap`, dropdowns become full-width panels on phones, touch targets are at least 44 px under 1024 px, inputs are 16 px to stop mobile zoom. |
| Accessibility | Skip-to-content link; `<main>`, `<nav aria-label>` landmarks; `aria-expanded` on the menu toggle, account menu and notification bell; ARIA tablist/tab/tabpanel with arrow-key navigation; `aria-pressed` on star buttons; decorative icons hidden from screen readers; `th scope="col"`; labelled search and file inputs; errors announced with `role="alert"`, confirmations with `role="status"`; Escape closes menus; contrast-checked text colours (all body text at least 4.5:1 on white and on the page background); `prefers-reduced-motion` and forced-colours support; `lang="en"`. |
| Colour and typography | Status colours are distinct hues (olive, blue, amber, violet, red) and every badge also carries a text label and dot, so meaning never depends on colour alone. |
| Feedback and system response | Flash messages after every action, toast messages for AJAX failures, spinner and disabled state on submitting buttons (restored when navigating back), server-side validation summaries, custom 403/404/error pages. |
| Performance | Static files served with cache-busting versions (`asp-append-version`), `font-display: swap`, server-side paging, no front-end framework, one small script (`wwwroot/js/app.js`), strict CSP with no inline scripts. |

## Hosting (9.4.3)

| Requirement | Evidence |
|---|---|
| Host the application | Own cPanel/WHM server: Kestrel runs as a systemd service behind Apache (HTTPS via AutoSSL): `docs/HOSTING.md`, `deploy/server-setup.sh`. Azure and Docker alternatives: `docs/HOSTING_AZURE.md`, `Dockerfile`. |
| Host the database and connect it | SQLite database on the server (`/var/lib/ug-crm/crm.db`), connected through `ConnectionStrings__Default` in a root-only settings file; created automatically on first start (`Data/DbSeeder.cs`); nightly backups. |
| Host the API | The `/api/v1` API runs in the same service as the website; `GET /api/v1/health` is the liveness probe. |
| Accessible, stable, responsive | HTTPS redirect + HSTS, production config (`appsettings.Production.json`), persistent Data Protection keys, `Restart=always` on crash and reboot, post-deploy smoke test. |

## GitHub and pipelines (9.4.4)

| Requirement | Evidence |
|---|---|
| Branch management | `main` / `develop` / `feature/*` / `bugfix/*` / `hotfix/*` model, protection rules, PR template: `docs/GITHUB_WORKFLOW.md`, `.github/pull_request_template.md`, `scripts/init-repo.ps1`. |
| Automated testing | `.github/workflows/ci.yml` builds and runs the xUnit tests on every push and pull request; failures block merging. |
| Automated deployment | `.github/workflows/deploy.yml` publishes and deploys to the server over SSH when `main` is updated, only after CI passes, then smoke-tests `/api/v1/health`. |
