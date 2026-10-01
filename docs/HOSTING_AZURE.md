# Hosting on Azure (alternative to cPanel)

> Optional route. The primary route is your own cPanel server: see `docs/HOSTING.md`. To use Azure, move `.github/workflows-optional/deploy-azure.yml` into `.github/workflows/` and remove `deploy.yml`.

The app is a single ASP.NET Core 9 service that serves the website **and** the JSON API (`/api/v1`) from the same host, so one deployment covers the front end, back end and API. The database is Azure SQL (or SQLite for a low-cost demo).

```
Browser / API client ──HTTPS──> Azure App Service (Linux, .NET 9)  ──> Azure SQL Database
                                  │  website (MVC views, wwwroot)
                                  └─ API  /api/v1/*   health: /api/v1/health
```

## 1. Create the Azure resources (once)

Replace the names in `<>`. Pick the region closest to your users that your subscription offers (e.g. `southafricanorth`).

```bash
az login
az group create -n rg-ug-crm -l southafricanorth

# Web app
az appservice plan create -n plan-ug-crm -g rg-ug-crm --is-linux --sku B1
az webapp create -n <unique-app-name> -g rg-ug-crm -p plan-ug-crm --runtime "DOTNETCORE:9.0"
az webapp update -n <unique-app-name> -g rg-ug-crm --https-only true
az webapp config set -n <unique-app-name> -g rg-ug-crm --generic-configurations '{"healthCheckPath":"/api/v1/health"}' --always-on true

# Database
az sql server create -n <unique-sql-server> -g rg-ug-crm -l southafricanorth -u sqladmin -p '<StrongPassword!>'
az sql db create -g rg-ug-crm -s <unique-sql-server> -n UG_CRM --service-objective Basic
az sql server firewall-rule create -g rg-ug-crm -s <unique-sql-server> -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0
```

## 2. Application settings (the connection between app and database)

Set these on the web app (Portal: *Settings > Environment variables*, or the CLI). They override `appsettings.json`; secrets never go into Git.

```bash
az webapp config appsettings set -n <unique-app-name> -g rg-ug-crm --settings \
  ASPNETCORE_ENVIRONMENT=Production \
  Database__Provider=SqlServer \
  "ConnectionStrings__Default=Server=tcp:<unique-sql-server>.database.windows.net,1433;Initial Catalog=UG_CRM;User ID=sqladmin;Password=<StrongPassword!>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;" \
  App__PublicBaseUrl=https://<unique-app-name>.azurewebsites.net \
  DataProtection__KeyPath=/home/keys \
  Seed__AdminEmail=<your admin email>
```

| Setting | Why |
|---|---|
| `Database__Provider=SqlServer` | Use Azure SQL instead of the SQLite default. Tables are created automatically on first start. |
| `ConnectionStrings__Default` | Database connection (stored as a secret in the App Service, not in the repo). |
| `App__PublicBaseUrl` | Correct links in password-reset, unsubscribe and tracking emails. |
| `DataProtection__KeyPath=/home/keys` | `/home` is persistent on App Service, so sign-in cookies and API tokens survive restarts and redeploys. |
| `Security:TrustForwardedHeaders` | Already `true` in `appsettings.Production.json` (App Service sits behind a proxy), so rate limiting sees real client IPs. |
| `Email__Host`, `Email__Port`, `Email__Username`, `Email__Password` | Optional: real SMTP. Without them emails are only logged. |

**First admin account.** With no `Seed__Users` configured, the first start creates one admin and writes a one-time password to the log (*Log stream*). Sign in and change it. 

**Demo data on the hosted site (for assessment).** To let a marker sign in to a populated system, add these settings, deploy, and remove them afterwards (the data stays):

```
Seed__DemoData=true
Seed__Users__0__FullName=System Administrator
Seed__Users__0__Email=admin@uncoveringgreatness.co.za
Seed__Users__0__Role=Admin
Seed__Users__0__Password=<a strong password you choose>
Seed__Users__1__FullName=Michaela
Seed__Users__1__Email=michaela@uncoveringgreatness.co.za
Seed__Users__1__Role=SalesRep
Seed__Users__1__Password=<a strong password you choose>
```
Seeding only runs while the database is empty. The demo accounts and passwords are only in `appsettings.Development.json`, so a real deployment never ships them.

## 3. Connect GitHub to Azure (once)

1. Portal: web app > *Configuration > General settings* > turn **SCM Basic Auth Publishing Credentials** On.
2. Download the publish profile: `az webapp deployment list-publishing-profiles -n <unique-app-name> -g rg-ug-crm --xml` (or *Overview > Get publish profile*).
3. GitHub repo > *Settings > Environments* > create **production** (and optionally **staging** with its own web app). In each environment add:
   - Secret `AZURE_WEBAPP_PUBLISH_PROFILE`: the full XML from step 2.
   - Variable `AZURE_WEBAPP_NAME`: the web app name.
   - Variable `APP_URL`: e.g. `https://<unique-app-name>.azurewebsites.net` (no trailing slash).
4. On **production**, add *Required reviewers* if you want a manual approval before each release.

From now on every merge to `main` deploys to production (see `docs/GITHUB_WORKFLOW.md`).

## 4. Verify the hosted environment

| Check | How |
|---|---|
| Site reachable over HTTPS | Open `https://<unique-app-name>.azurewebsites.net` (HTTP redirects to HTTPS). |
| API + database connected | `GET /api/v1/health` returns `{"status":"ok"}`; signing in proves the database works. |
| API from outside | Run the requests in `tests/api.http` or the smoke test against the hosted URL. |
| Stable | App Service *Always on* and *Health check* are enabled; *Log stream* shows application logs. |

## Alternative: container or low-cost SQLite hosting

- `Dockerfile` builds a runnable image (`docker build -t ug-crm .`). Run with a volume: `docker run -p 8080:8080 -v ug-data:/data ug-crm`. The SQLite file and keys live on `/data`.
- On Azure App Service, if you skip Azure SQL, leave `Database__Provider` unset and set `ConnectionStrings__Default=Data Source=/home/crm.db`. This is fine for a demo with one instance, but use Azure SQL for anything real.
- Scaling beyond one instance needs Azure SQL, a shared Data Protection key store, and a shared cache/limiter (rate limiting and the user-state cache are in memory).
