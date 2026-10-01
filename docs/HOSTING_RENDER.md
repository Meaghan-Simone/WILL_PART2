# Hosting on Render's free plan

Render has no native .NET runtime, so it builds the app from the `Dockerfile` in this repo. The website and the `/api/v1` API are one service, with HTTPS and a `*.onrender.com` address included.

## Free plan limits (read before choosing it)
| Limit | What it means for this app |
|---|---|
| Sleeps after 15 minutes without traffic | The first request after idle takes about a minute (Render shows a loading page). Open the site before a demo or marking session. |
| **No persistent disk** | The SQLite database is wiped on every restart, redeploy and wake-up. With `Seed__DemoData=true` it is recreated with demo data each time; anything users add is lost. |
| Monthly free instance hours | One always-awake service uses about the whole allowance; a sleeping service uses none. |
| Render may restart it at any time | Same effect as above on the database. |

Render's free Postgres expires after 30 days and this app has no PostgreSQL support, so it is not a fix. To keep real data for free you would need an external database; the app already supports SQL Server (`Database__Provider=SqlServer`), for example Azure SQL Database's free offer if your account has it (allow Render's outbound IPs in its firewall). For real use, a paid Render instance with a persistent disk is the simple option.

## Steps
1. Push the repo to GitHub (see `docs/GITHUB_WORKFLOW.md`).
2. Render dashboard > **New > Blueprint** > select the repo. It reads `render.yaml` and creates the free web service `ug-crm`.
3. When asked, enter:
   - `Seed__Users__0__Password`: a strong admin password (10+ characters, upper/lower/number/symbol)
   - `App__PublicBaseUrl`: `https://<your-service-name>.onrender.com` (the exact URL is shown on the service page; update the value if it differs)
4. Wait for the first build (a few minutes). Then open the URL, or check `https://<your-service>.onrender.com/api/v1/health`.
5. Sign in as `admin@uncoveringgreatness.co.za` with the password from step 3. With demo data on, the other demo accounts exist too (password `P@ssword4321`, see the README), so **use fake data only**. Set `Seed__DemoData` to `false` if you do not want those accounts on a public address.

## Automatic deployment from GitHub (only after tests pass)
`render.yaml` turns off Render's own auto-deploy; GitHub Actions deploys instead.
1. Render > service > **Settings > Deploy Hook**: copy the URL.
2. GitHub repo > Settings > **Secrets and variables > Actions**:
   - Repository **variable** `DEPLOY_TARGET` = `render`
   - Environment **production** > secret `RENDER_DEPLOY_HOOK` = the hook URL; variable `APP_URL` = `https://<your-service>.onrender.com` (no trailing slash)
3. Merge to `main`: CI runs, then the workflow triggers the hook and waits for `/api/v1/health` to answer.

Because the free service sleeps, the smoke test can take a few minutes on a cold start; the workflow retries for about 10 minutes.
