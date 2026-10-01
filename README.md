# Uncovering Greatness CRM

ASP.NET Core 9 MVC application with a JSON API, EF Core persistence and role-based security.
It turns the original screens (dashboard, pipeline, leads, companies, events, tasks, calendar, campaigns, users,
notifications, login) into a working system while keeping the existing design.

## Run it

```bash
dotnet run          # http://localhost:5080 (see Properties/launchSettings.json)
```

* Demo data and demo accounts live in `appsettings.Development.json` (used by `dotnet run` / `launchSettings.json`). `appsettings.json` and `appsettings.Production.json` ship with none, so a hosted deployment never contains known passwords.
* Database: SQLite file at `App_Data/crm.db`, created automatically on first start (no setup).
* **Demo data (for testing):** `Seed:DemoData` is `true` in `appsettings.Development.json`. On first start with an EMPTY database the app loads
  10 companies, 5 events, 36 leads, tasks, calendar entries, notifications, campaigns, a form connection with sample submissions
  and an audit trail. Each person gets their own data (leads, tasks, calendar, notifications), so you can sign in as each user and
  see a different workspace. All dates are relative to today, so the demo always looks current.
  **To reload it: stop the app, delete `App_Data/crm.db`, start again.** It is `false` in production by default.
* **Demo accounts**, all with password `P@ssword4321`:

  | Email | Role | What to test with it |
  |---|---|---|
  | `admin@uncoveringgreatness.co.za` | Admin | Everything: users, audit log, campaigns, events, form integrations |
  | `michaela@uncoveringgreatness.co.za` | Sales rep | 11 leads (consulting / finance / tech), tasks, overdue follow-up, a won deal |
  | `sule@uncoveringgreatness.co.za` | Sales rep | 11 leads (healthcare / education / hospitality), an overdue proposal |
  | `barry@uncoveringgreatness.co.za` | Sales rep | 11 leads (mining / retail / logistics / energy) |
  | `thabo@uncoveringgreatness.co.za` | Staff | Read-only: every create/edit is refused, and sees no leads (none are assigned to staff) |
  | `lerato@uncoveringgreatness.co.za` | Sales rep | New hire: must change password at first sign-in |
  | `pieter@uncoveringgreatness.co.za` | Sales rep | Locked out (admin: Users, then Unlock) |
  | `nomsa@uncoveringgreatness.co.za` | Sales rep | Deactivated: sign-in is refused |

  Three leads are unassigned so you can test assigning. Two leads are opted out of marketing, so campaigns skip them.
  `Seed:MustChangePassword` can force everyone to change password at first sign-in.
* If `Seed:Users` is empty, one admin is created with a random one-time password written to the log.
* Seeding only happens when the database has no users (accounts) and no leads/companies/events (demo records).
* The demo passwords exist only in `appsettings.Development.json` and are never part of a production deployment.

### Switching to SQL Server
```json
"Database": { "Provider": "SqlServer" },
"ConnectionStrings": { "Default": "Server=...;Database=UG_CRM;Trusted_Connection=True;TrustServerCertificate=True" }
```
For real schema migrations: `dotnet ef migrations add Initial`, then set `Database:UseMigrations` to `true`.

### Email
Without `Email:Host` emails are only logged (password resets, campaigns) so nothing leaves your machine.
Set the `Email` section (SMTP host, port, credentials) to send for real, and set `App:PublicBaseUrl` to your public
HTTPS address so links inside emails (reset, unsubscribe, open tracking) are correct.

## Build and test

```powershell
.\build-and-test.ps1          # restore, build, run the unit tests
.\build-and-test.ps1 -Run     # ...then start the app on http://localhost:5080
```
The unit tests live in `tests/UnitTests` (xUnit). `tests/api-smoke-test.ps1` checks the running API. `docs/ASSESSMENT_MAPPING.md` maps the code to the assessment criteria.

## Hosting and CI/CD

* `docs/HOSTING.md`: deploy to your own cPanel/WHM server (`deploy/server-setup.sh`). `docs/HOSTING_RENDER.md` is the free Render route (`render.yaml`), `docs/HOSTING_AZURE.md` the Azure alternative.
* `docs/GITHUB_WORKFLOW.md`: branch model, protection rules, and the CI (`ci.yml`) and deploy (`deploy.yml`, to the cPanel server) pipelines.

## Troubleshooting

* **"Failed to bind to address https://127.0.0.1:7080: address already in use"**: a previous run of the app is still going (often a debug
  session that was not stopped). Stop it (Visual Studio: Debug > Stop Debugging; or in a terminal
  `taskkill /IM UncoveringGreatnessCRM.exe /F`), or change the ports in `Properties/launchSettings.json`, then run again.
* **No demo users when running outside Visual Studio**: demo accounts load only in the Development environment. Run `$env:ASPNETCORE_ENVIRONMENT="Development"; dotnet run` or use `.\build-and-test.ps1 -Run`.
* **Want a fresh start**: the data lives in `App_Data/crm.db`. Stop the app, delete that file and start again. With `Seed:DemoData` on you
  get the demo data again; with it off you get an empty system with only the configured users.

## Roles

| | Admin | Sales rep | Staff |
|---|---|---|---|
| View companies, events, own calendar | ✔ | ✔ | ✔ |
| View leads, pipeline, search, dashboard figures | all leads | only leads assigned to them | only leads assigned to them (staff cannot be assigned leads, so none) |
| Create leads/companies, tasks, log calls, notes | ✔ | ✔ (a rep's new lead is always assigned to them) | – |
| Edit / move / reassign a lead | any | only leads assigned to them (they cannot unassign; a lead they hand over disappears from their list) | – |
| Delete leads, companies | ✔ | – | – |
| Manage events, campaigns, users, audit log | ✔ | – | – |

## Security features

* **Authentication**: cookie sessions (HttpOnly, SameSite=Lax, `__Host-` prefix + Secure in production, sliding 60 min) for the web UI;
  short-lived tamper-proof **bearer tokens** (Data Protection, 8 h) for the API.
* **Passwords**: PBKDF2 (ASP.NET Core Identity hasher, auto-upgrades), policy of 10+ chars with upper/lower/number/symbol,
  no name/email inside the password, common passwords blocked. Timing-safe handling of unknown accounts.
* **Brute-force protection**: 5 failed sign-ins locks the account for 15 min (admins can unlock) plus per-IP rate limiting on
  sign-in/reset endpoints and a global per-user/IP limiter.
* **Session invalidation**: every user has a security stamp. Password change/reset, role change or deactivation ends all
  existing sessions and API tokens immediately.
* **Forced first-login password change** for accounts created or reset by an admin. API tokens for such accounts can only reach
  `auth/me`, `auth/change-password` and `auth/revoke` until the password is changed.
* **Admin force sign-out**: revoke any user's sessions and API tokens at once (`POST users/{id}/revoke-sessions`).
* **Password reset** by expiring (1 h), single-use, signed link; identical response whether or not the email exists.
* **Authorisation**: deny-by-default (fallback policy), role policies, and record-level rules enforced in the service layer so the
  web UI and API can never disagree. Last active admin cannot be removed; admins cannot lock themselves out.
* **CSRF**: antiforgery on all state-changing web requests; the API accepts bearer tokens only, so it is not CSRF-exposed.
* **Headers**: strict CSP (no inline scripts), HSTS, X-Frame-Options DENY, nosniff, Referrer-Policy, Permissions-Policy, no-store on pages.
* **Input handling**: server-side validation everywhere, parameterised queries via EF Core, HTML-encoded output, URL scheme checks,
  CSV export formula-injection guard, 5 MB CSV import cap.
* **Audit log**: sign-ins (success/failure/lockout), password events, user/role changes, deletes, exports and imports.
* **Marketing compliance**: one-click unsubscribe (POST-confirmed), opted-out leads are always excluded from campaigns.

## API (`/api/v1`, JSON)

```bash
# 1. get a token
curl -X POST http://localhost:5080/api/v1/auth/token -H "Content-Type: application/json" \
     -d '{"email":"michaela@uncoveringgreatness.co.za","password":"P@ssword4321"}'
# 2. use it
curl http://localhost:5080/api/v1/leads?stage=Qualified -H "Authorization: Bearer <accessToken>"
```

| Area | Endpoints |
|---|---|
| Auth | `POST auth/token` (sign in), `POST auth/refresh`, `GET auth/me`, `POST auth/change-password` (`currentPassword`, `newPassword`), `POST auth/revoke` (sign out everywhere), `POST auth/forgot-password`, `POST auth/reset-password` |
| Health | `GET health` (anonymous liveness check) |
| Leads | `GET/POST leads`, `GET/PUT/DELETE leads/{id}`, `PATCH leads/{id}/stage`, `PATCH leads/{id}/assignee`, `POST leads/{id}/notes`, `POST leads/{id}/calls`, `POST leads/{id}/star`, `GET leads/pipeline`, `GET leads/export` |
| Companies | `GET/POST companies`, `GET/PUT/DELETE companies/{id}` |
| Events | `GET/POST events`, `GET/PUT/DELETE events/{id}` |
| Tasks | `GET/POST tasks`, `PUT/DELETE tasks/{id}`, `PATCH tasks/{id}/complete` |
| Calendar | `GET calendar?year=&month=`, `POST calendar`, `GET/PUT/DELETE calendar/{id}` |
| Campaigns (admin) | `GET/POST campaigns`, `GET/PUT/DELETE campaigns/{id}`, `POST campaigns/{id}/send`, `GET campaigns/audience` |
| Users (admin) | `GET/POST users`, `GET/PUT users/{id}`, `DELETE users/{id}?transferToId=`, `POST users/{id}/reset-password`, `POST users/{id}/unlock`, `POST users/{id}/revoke-sessions` (force sign-out), `GET audit` |
| Form integrations (admin) | `GET/POST integrations/forms`, `GET/PUT/DELETE integrations/forms/{id}`, `POST integrations/forms/{id}/rotate-token`, `POST integrations/forms/{id}/preview` |
| Other | `GET dashboard`, `GET search?q=`, `GET team`, `GET notifications`, `POST notifications/{id}/read`, `POST notifications/read-all` |

List endpoints take `page`, `pageSize` and filters (leads: `q, stage, eventId, assignedToId, companyId, view=mine|followup|starred, sort`)
and return `{ items, page, pageSize, totalCount, totalPages }`. Errors are `{ "error": "...", "errors": { "Field": ["..."] } }`
with 400 / 401 / 403 / 404 / 409 / 423 / 429.

## Testing

* `tests/api-smoke-test.ps1`: signs in as every demo account and checks sign-in, lockout, forced password change, role rules,
  record-level rules, token refresh/revoke and the main create/update/delete endpoints
  (`powershell -ExecutionPolicy Bypass -File tests\api-smoke-test.ps1`). It cleans up after itself, so it can be re-run.
* `tests/api.http`: the same requests for manual use in Visual Studio 2022 or VS Code (REST Client).

## Linking Tally / Google Forms (Form integrations)

Admins: **Form integrations → Connect a form**. Each connection gets a private webhook URL
(`/integrations/forms/<token>`). When someone submits the form, the tool POSTs it to that URL and a lead appears in the CRM within a
second, assigned to your team and with the owner notified. This is push, not polling: nothing has to be checked on a schedule.

* **Tally**: form → Integrations → Webhooks → paste the URL. Optionally enable a signing secret in Tally and enter the same secret on the connection;
  deliveries without a valid `Tally-Signature` are then rejected.
* **Google Forms**: Google has no webhooks, so the connection page generates a short Apps Script to paste into the form (Script editor, run `setup()` once).
* **Anything else** (Zapier, Make, your own site): POST JSON such as `{"fields":[{"label":"Email","value":"a@b.co"}]}` or a flat `{"Email":"a@b.co","Name":"Ann Lee"}`.
* **Field mapping is automatic**: questions labelled Email, Name / First name / Surname, Phone, Company, Job title, Industry, City, Province,
  LinkedIn, Message are recognised. Anything else is kept in the lead's activity. Override with lines like `Email = Work email` if your labels differ.
  Use **Test how a submission is read** on the connection page to check a sample payload without saving anything.
* **Assignment**: take turns across admins and reps (default), whoever has the fewest open leads, or one named person. Optionally link every lead to an event and add tags.
* **Safe by design**: unguessable URL (rotatable), optional HMAC signature, 256 KB body cap, per-connection rate limit, retried deliveries are ignored
  (de-duplicated by the form's response id), an existing email never creates a second lead (the submission is added to its activity instead), a submission
  with no valid email is rejected and logged, all text is length-limited and HTML-encoded on display. Only a name/email summary of each submission is kept, not the raw answers.
* **Requirement**: the URL must be reachable from the internet. On `localhost` Tally and Google cannot reach it: deploy over HTTPS and set `App:PublicBaseUrl`
  (or use a tunnel such as ngrok while testing).

## User management (admins)

* **Add**: name, email, job title, phone, role and a generated temporary password (the user must change it at first sign-in).
* **Edit everything**: name, email (the sign-in name, must stay unique), job title, phone, role, active/inactive, plus reset password and unlock.
  Role/status changes, or changing someone else's email, sign that user out everywhere.
* **Delete**: permanent. You choose who takes over the user's leads and assigned tasks. Notes, campaigns and activity they wrote are kept and
  shown as written by a deleted user; their personal calendar events, notifications and stars are removed. All in one transaction and audit-logged.
* Safeguards: you can't delete or deactivate yourself, and the last active admin can't be deleted, demoted or deactivated.
  To stop someone signing in without losing anything, untick "Account is active" instead of deleting.
* Upgrading from an earlier build? The user table gained columns, so delete `App_Data/crm.db` and restart to recreate it.

## Behaviour worth knowing

* New leads with no owner are auto-assigned to the sales rep with the fewest open leads.
* A background worker creates in-app reminders for due/overdue follow-ups and tasks (once per item per day).
* Lead follow-up dates appear on the assignee's calendar automatically; calendar events are private, shared or team-wide.
* Campaigns send in the background, personalise `{{FirstName}}`, `{{FullName}}`, `{{Company}}`, track opens, and resume after a restart.
* Dates entered by users are treated as South African time (SAST); system timestamps are stored in UTC.

## Project layout

```
Domain/        entities + enums          Data/       DbContext + seeder
Services/      all business rules        Security/   auth, tokens, policies, headers
Controllers/   MVC controllers           Controllers/Api/   JSON API
Views/         Razor views (original design)   wwwroot/   css, js, images
```
