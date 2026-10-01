#!/usr/bin/env bash
# One-time setup of the Uncovering Greatness CRM on a cPanel/WHM server (root SSH access required).
#
#   DOMAIN=crm.example.co.za CPUSER=examplecp ADMIN_EMAIL=you@example.co.za bash server-setup.sh
#
# Before running: in cPanel, create the subdomain (DOMAIN) and let AutoSSL issue its certificate.
set -euo pipefail
: "${DOMAIN:?set DOMAIN, e.g. crm.example.co.za}"
: "${CPUSER:?set CPUSER, the cPanel account that owns the domain}"
: "${ADMIN_EMAIL:?set ADMIN_EMAIL for the first admin account}"
PORT="${PORT:-5080}"
APP_DIR=/var/www/ug-crm
DATA_DIR=/var/lib/ug-crm

[ "$(id -u)" -eq 0 ] || { echo "Run as root."; exit 1; }

echo "== 1. Packages and .NET 9 runtime =="
if command -v dnf >/dev/null; then dnf install -y libicu sqlite rsync curl
elif command -v apt-get >/dev/null; then apt-get update && apt-get install -y libicu-dev sqlite3 rsync curl; fi
if [ ! -x /opt/dotnet/dotnet ]; then
  curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh
  bash /tmp/dotnet-install.sh --channel 9.0 --runtime aspnetcore --install-dir /opt/dotnet
fi
ln -sf /opt/dotnet/dotnet /usr/local/bin/dotnet
dotnet --list-runtimes

echo "== 2. Users and folders =="
NOLOGIN="$(command -v nologin || echo /sbin/nologin)"
id ugcrm >/dev/null 2>&1 || useradd --system --home-dir "$DATA_DIR" --shell "$NOLOGIN" ugcrm
id deploy >/dev/null 2>&1 || useradd -m -s /bin/bash deploy
mkdir -p "$APP_DIR/App_Data" "$DATA_DIR/keys"
chown -R deploy:ugcrm "$APP_DIR"; chmod 755 "$APP_DIR"
chown ugcrm:ugcrm "$APP_DIR/App_Data"; chmod 750 "$APP_DIR/App_Data"
chown -R ugcrm:ugcrm "$DATA_DIR"; chmod -R 750 "$DATA_DIR"

echo "== 3. Settings file (/etc/ug-crm.env) =="
if [ ! -f /etc/ug-crm.env ]; then
cat > /etc/ug-crm.env <<ENV
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:${PORT}
ConnectionStrings__Default=Data Source=${DATA_DIR}/crm.db
DataProtection__KeyPath=${DATA_DIR}/keys
App__PublicBaseUrl=https://${DOMAIN}
Seed__AdminEmail=${ADMIN_EMAIL}
# Optional real email (otherwise emails are only written to the log):
# Email__Host=mail.example.co.za
# Email__Port=587
# Email__Username=no-reply@example.co.za
# Email__Password=
ENV
chmod 600 /etc/ug-crm.env
fi

echo "== 4. systemd service =="
cat > /etc/systemd/system/ug-crm.service <<UNIT
[Unit]
Description=Uncovering Greatness CRM
After=network.target

[Service]
WorkingDirectory=${APP_DIR}
ExecStart=/usr/local/bin/dotnet ${APP_DIR}/UncoveringGreatnessCRM.dll
EnvironmentFile=/etc/ug-crm.env
User=ugcrm
Group=ugcrm
Restart=always
RestartSec=5
SyslogIdentifier=ug-crm
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full

[Install]
WantedBy=multi-user.target
UNIT
systemctl daemon-reload
systemctl enable ug-crm

SYSTEMCTL="$(command -v systemctl)"
echo "deploy ALL=(root) NOPASSWD: ${SYSTEMCTL} restart ug-crm, ${SYSTEMCTL} status ug-crm" > /etc/sudoers.d/ug-crm
chmod 440 /etc/sudoers.d/ug-crm

echo "== 5. Apache reverse proxy for ${DOMAIN} =="
for MODE in std ssl; do
  PROTO=http; [ "$MODE" = ssl ] && PROTO=https
  DIR="/etc/apache2/conf.d/userdata/${MODE}/2_4/${CPUSER}/${DOMAIN}"
  mkdir -p "$DIR"
  cat > "${DIR}/ug-crm.conf" <<CONF
# Forward everything to the ASP.NET Core app, except certificate validation (AutoSSL) paths
ProxyPreserveHost On
ProxyRequests Off
ProxyPass /.well-known !
ProxyPass / http://127.0.0.1:${PORT}/ retry=0 timeout=60
ProxyPassReverse / http://127.0.0.1:${PORT}/
RequestHeader set X-Forwarded-Proto "${PROTO}"
CONF
done
/scripts/rebuildhttpdconf
/scripts/restartsrv_httpd

echo "== 6. Nightly database backup =="
mkdir -p /var/backups/ug-crm
cat > /etc/cron.d/ug-crm-backup <<CRON
15 2 * * * root [ -f ${DATA_DIR}/crm.db ] && sqlite3 ${DATA_DIR}/crm.db ".backup '/var/backups/ug-crm/crm-\$(date +\%F).db'" && find /var/backups/ug-crm -name 'crm-*.db' -mtime +14 -delete
CRON

cat <<DONE

Server is ready. Next:
  1. Add your deploy SSH public key to /home/deploy/.ssh/authorized_keys
  2. Push to main (GitHub Actions deploys) or copy a publish folder to ${APP_DIR}
  3. First admin password:  journalctl -u ug-crm | grep "FIRST RUN"
  4. Check:  curl https://${DOMAIN}/api/v1/health
DONE
