# Hosting on your own cPanel server (9.4.3)

Works when you have **root SSH access to the server** that runs cPanel/WHM (a VPS or dedicated server). It does not work on a locked-down shared hosting account.

```
Browser / API client ──HTTPS──> Apache (cPanel, AutoSSL certificate for crm.yourdomain)
                                   │ reverse proxy
                                   └──> Kestrel (the CRM, 127.0.0.1:5080, systemd service "ug-crm")
                                           └──> SQLite database  /var/lib/ug-crm/crm.db
```
The website and the `/api/v1` API are the same process, so one deployment covers front end, back end and API. Port 5080 is only reachable from the server itself; the public only talks to Apache over HTTPS.

## 1. Prepare the domain (cPanel)
1. cPanel > **Domains** (or Subdomains): create `crm.yourdomain.co.za`.
2. cPanel > **SSL/TLS Status**: run AutoSSL for it so it has a valid certificate.

## 2. Set up the server (once, as root)
Copy the `deploy/` folder to the server, then:

```bash
DOMAIN=crm.yourdomain.co.za CPUSER=<cpanel username> ADMIN_EMAIL=<you@example.co.za> bash deploy/server-setup.sh
```
The script installs the .NET 9 runtime, creates the `ugcrm` (runs the app) and `deploy` (receives uploads) users, writes `/etc/ug-crm.env` (settings), installs the `ug-crm` systemd service, adds the Apache proxy include for your domain (keeping AutoSSL's `/.well-known` path working), and sets a nightly database backup (kept 14 days in `/var/backups/ug-crm`).

Requirements: AlmaLinux/CloudLinux/Rocky or Ubuntu as supported by cPanel, Apache with `mod_proxy`, `mod_proxy_http` and `mod_headers` (included in EasyApache 4). If your server uses Nginx as the front web server, proxy `/` to `http://127.0.0.1:5080` and send `X-Forwarded-Proto` instead.

## 3. Database and settings
- The database is a SQLite file on the server (`ConnectionStrings__Default` in `/etc/ug-crm.env`). Tables are created automatically on first start, and it lives outside the deploy folder so deployments never overwrite it.
- Data Protection keys (sign-in cookies, API tokens) are stored in `/var/lib/ug-crm/keys`, so users stay signed in across restarts and deployments.
- `/etc/ug-crm.env` is readable by root only. Edit it to add SMTP (`Email__*`) settings, then `systemctl restart ug-crm`.
- **First admin**: `journalctl -u ug-crm | grep "FIRST RUN"` shows the one-time password; change it at first sign-in.
- **Demo data for markers**: add these lines to `/etc/ug-crm.env` before the first start (seeding only happens while the database is empty), restart, then remove the passwords from the file:
  ```
  Seed__DemoData=true
  Seed__Users__0__FullName=System Administrator
  Seed__Users__0__Email=admin@yourdomain.co.za
  Seed__Users__0__Role=Admin
  Seed__Users__0__Password=<strong password>
  Seed__Users__1__FullName=Michaela
  Seed__Users__1__Email=michaela@yourdomain.co.za
  Seed__Users__1__Role=SalesRep
  Seed__Users__1__Password=<strong password>
  ```
- MySQL (cPanel's own database) is **not** supported by the code; SQLite is the supported option here.

## 4. Connect GitHub to the server (once)
1. On your computer: `ssh-keygen -t ed25519 -f ug-deploy -N ""`.
2. On the server, append `ug-deploy.pub` to `/home/deploy/.ssh/authorized_keys` (folder mode 700, file mode 600, owned by `deploy`).
3. Get the host key line for the secret: `ssh-keyscan -p 22 <server-ip-or-host>`.
4. GitHub repo > Settings > Environments > **production**:
   - Secret `SSH_PRIVATE_KEY`: contents of `ug-deploy`
   - Secret `SSH_KNOWN_HOSTS`: output of step 3
   - Variable `SSH_HOST`: server hostname or IP; optional `SSH_PORT` (default 22) and `SSH_USER` (default `deploy`)
   - Variable `APP_URL`: `https://crm.yourdomain.co.za` (no trailing slash)

Every merge to `main` now tests, copies the published app to `/var/www/ug-crm`, restarts the service and checks `/api/v1/health` (see `docs/GITHUB_WORKFLOW.md`).

## 5. Manual deploy (first time, or without GitHub)
```bash
dotnet publish UncoveringGreatnessCRM.csproj -c Release -o publish
rsync -az --delete --exclude 'App_Data/' publish/ deploy@<server>:/var/www/ug-crm/
ssh deploy@<server> "sudo systemctl restart ug-crm"
```

## 6. Verify and operate
| Check | Command / action |
|---|---|
| Service running | `systemctl status ug-crm` |
| Logs | `journalctl -u ug-crm -f` |
| Site over HTTPS | open `https://crm.yourdomain.co.za` (HTTP redirects to HTTPS) |
| API + database | `curl https://crm.yourdomain.co.za/api/v1/health` returns `{"status":"ok",...}`; signing in proves the database works |
| Restart after crash/reboot | automatic (`Restart=always`, service enabled at boot) |
| Restore a backup | stop the service, copy a file from `/var/backups/ug-crm` to `/var/lib/ug-crm/crm.db`, `chown ugcrm:ugcrm`, start the service |

**Limits to mention in your write-up:** one server and one app instance (rate limiting and the session cache are in memory), and SQLite suits a small team, not heavy concurrent write loads.
