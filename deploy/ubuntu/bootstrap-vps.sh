#!/usr/bin/env bash
# KitPlatform Novixa — bootstrap VPS (Ubuntu 24.04)
# Chay sau upload: bash /tmp/kit-platform-upload/deploy/ubuntu/bootstrap-vps.sh
#
# Env tuy chon:
#   CERTBOT_EMAIL=care@novixa.vn   — bo qua certbot neu khong set
#   SKIP_CERTBOT=1                 — chi HTTP, khong SSL
set -euo pipefail

DOMAIN="${DOMAIN:-novixa.vn}"
API_HOST="api.${DOMAIN}"
ADMIN_HOST="admin.${DOMAIN}"
APP_HOST="app.${DOMAIN}"
POS_HOST="pos.${DOMAIN}"
SURVEY_HOST="survey.${DOMAIN}"
PRESCRIBER_HOST="prescriber.${DOMAIN}"
PARTNER_HOST="partner.${DOMAIN}"
UPLOAD="${UPLOAD:-/tmp/kit-platform-upload}"
WEB_ROOT="/var/www/kit-platform"
CONFIG_DIR="/etc/kit-platform"
OPT="/opt/kit-platform"
DB_NAME="novixa_prod"
DB_USER="kitplatform"
SECRETS_FILE="${CONFIG_DIR}/secrets.generated"

log() { echo -e "\n\033[1;36m==>\033[0m $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "Chay bang root: sudo bash $0"

[[ -d "$UPLOAD/api" ]] || die "Chua upload — chay scripts/upload-to-vps.ps1 tu may dev"

# --- 1. Packages ---
log "Cai goi he thong..."
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get upgrade -y -qq
apt-get install -y -qq curl wget gnupg2 ca-certificates lsb-release \
  nginx certbot python3-certbot-nginx \
  postgresql postgresql-contrib ufw rsync unzip python3

# --- 2. .NET 10 ---
if ! dotnet --list-runtimes 2>/dev/null | grep -q "Microsoft.AspNetCore.App 10"; then
  log "Cai ASP.NET Core 10 runtime..."
  wget -q https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
  dpkg -i /tmp/packages-microsoft-prod.deb
  apt-get update -qq
  apt-get install -y -qq aspnetcore-runtime-10.0
fi
dotnet --list-runtimes | grep AspNetCore || die ".NET 10 runtime chua cai"

# --- 3. Firewall ---
log "Cau hinh UFW..."
ufw allow OpenSSH >/dev/null 2>&1 || true
ufw allow 'Nginx Full' >/dev/null 2>&1 || true
ufw --force enable

# --- 4. PostgreSQL ---
if [[ ! -f "$SECRETS_FILE" ]]; then
  DB_PASS="$(openssl rand -base64 24 | tr -d '/+=' | head -c 32)"
  JWT_SECRET="$(openssl rand -base64 48 | tr -d '\n')"
  PLATFORM_KEY="$(openssl rand -base64 24 | tr -d '/+=' | head -c 24)"
  mkdir -p "$CONFIG_DIR"
  cat >"$SECRETS_FILE" <<EOF
DB_PASS=$DB_PASS
JWT_SECRET=$JWT_SECRET
PLATFORM_KEY=$PLATFORM_KEY
EOF
  chmod 600 "$SECRETS_FILE"
  log "Da tao secret moi -> $SECRETS_FILE (LUU FILE NAY!)"
else
  # shellcheck disable=SC1090
  source "$SECRETS_FILE"
  log "Dung lai secret tu $SECRETS_FILE"
fi

if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='$DB_USER'" | grep -q 1; then
  log "Tao PostgreSQL user + database..."
  sudo -u postgres psql -v ON_ERROR_STOP=1 <<EOF
CREATE ROLE $DB_USER WITH LOGIN PASSWORD '$DB_PASS';
CREATE DATABASE $DB_NAME OWNER $DB_USER;
GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;
EOF
else
  log "PostgreSQL user $DB_USER da ton tai — bo qua tao DB"
fi

# --- 5. Copy artifacts ---
log "Copy publish/ -> $WEB_ROOT ..."
mkdir -p "$WEB_ROOT" "$OPT" "$WEB_ROOT/api/uploads"
rsync -a --delete "$UPLOAD/api/" "$WEB_ROOT/api/"
rsync -a --delete "$UPLOAD/admin/" "$WEB_ROOT/admin/"
rsync -a --delete "$UPLOAD/customer-app/" "$WEB_ROOT/customer-app/"
if [[ -d "$UPLOAD/staff-app" ]]; then
  rsync -a --delete "$UPLOAD/staff-app/" "$WEB_ROOT/staff-app/"
else
  log "Canh bao: chua co staff-app/ trong upload — bo qua POS mobile"
fi
rsync -a "$UPLOAD/deploy/ubuntu/" "$OPT/"
rsync -a "$UPLOAD/migrations/" "$OPT/migrations/"
chmod +x "$OPT"/*.sh 2>/dev/null || true
chown -R www-data:www-data "$WEB_ROOT"

# --- 6. Migrations ---
log "Chay migration production..."
CONN="postgresql://${DB_USER}:${DB_PASS}@127.0.0.1:5432/${DB_NAME}"
bash "$OPT/run-migrations-prod.sh" "$CONN"

# --- 7. api.env ---
log "Tao /etc/kit-platform/api.env ..."
cat >"${CONFIG_DIR}/api.env" <<EOF
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://127.0.0.1:5000

ConnectionStrings__Default=Host=127.0.0.1;Port=5432;Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASS}

Jwt__Secret=${JWT_SECRET}

Cors__AllowedOrigins__0=https://${ADMIN_HOST}
Cors__AllowedOrigins__1=https://${APP_HOST}
Cors__AllowedOrigins__2=https://${POS_HOST}
Cors__AllowedOrigins__3=https://${SURVEY_HOST}
Cors__AllowedOrigins__4=https://${PRESCRIBER_HOST}
Cors__AllowedOrigins__5=https://${PARTNER_HOST}

CustomerAppAuth__ExposePilotOtpInAdmin=false
CustomerAppAuth__ExposePilotOtpOnCustomerApp=false

Platform__BrandName=Novixa
Platform__ProductName=ERP Nha thuoc
Platform__AdminUrl=https://${ADMIN_HOST}
Platform__CustomerAppUrl=https://${APP_HOST}
Platform__ApiUrl=https://${API_HOST}
Platform__ProvisioningKey=${PLATFORM_KEY}

CustomerAppSms__Provider=Http
CustomerAppSms__HttpUrl=http://127.0.0.1:9091/sms
CustomerAppSms__ApiKeyHeader=Authorization
CustomerAppSms__ApiKey=
CustomerAppSms__MessageTemplate=Ma OTP Novixa cua ban la {code}. Hieu luc {minutes} phut.

CustomerAppPush__Enabled=false
CustomerAppPush__PollIntervalSeconds=60
CustomerAppPush__Subject=mailto:care@${DOMAIN}
CustomerAppPush__PublicKey=
CustomerAppPush__PrivateKey=

NationalDrugCatalog__Mode=mock
EOF
chmod 600 "${CONFIG_DIR}/api.env"

# --- 8. SMS stub (pilot) ---
log "Cai SMS OTP stub (pilot)..."
cp "$OPT/sms-otp-stub.py" "$OPT/"
cp "$OPT/kit-platform-sms-stub.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable --now kit-platform-sms-stub

# --- 9. systemd API ---
log "Cai kit-platform-api.service ..."
cp "$OPT/kit-platform-api.service" /etc/systemd/system/
systemctl daemon-reload
systemctl enable kit-platform-api
systemctl restart kit-platform-api
sleep 2
systemctl is-active --quiet kit-platform-api || {
  journalctl -u kit-platform-api -n 30 --no-pager
  die "kit-platform-api khong khoi dong"
}

# --- 10. Nginx ---
log "Cau hinh Nginx..."
sed "s/novixa\.vn/${DOMAIN}/g" "$OPT/nginx-kit-platform.conf" > /etc/nginx/sites-available/kit-platform
ln -sf /etc/nginx/sites-available/kit-platform /etc/nginx/sites-enabled/kit-platform
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

# --- 11. Certbot (optional) ---
if [[ "${SKIP_CERTBOT:-}" != "1" && -n "${CERTBOT_EMAIL:-}" ]]; then
  log "Certbot SSL cho ${API_HOST}, ${ADMIN_HOST}, ${APP_HOST}, ${POS_HOST}..."
  if certbot --nginx \
    -d "$API_HOST" -d "$ADMIN_HOST" -d "$APP_HOST" -d "$POS_HOST" \
    --non-interactive --agree-tos -m "$CERTBOT_EMAIL" --redirect; then
    log "SSL OK"
  else
    log "Certbot that bai — kiem tra DNS A record tro ve IP VPS. Tiep tuc HTTP."
  fi
else
  log "Bo qua Certbot (dat CERTBOT_EMAIL=... hoac bo SKIP_CERTBOT=1 de bat SSL)"
fi

# --- 12. Smoke test ---
log "Smoke test..."
if bash "$OPT/smoke-test.sh" 2>/dev/null; then
  log "Smoke test passed (HTTP)"
else
  curl -sf "http://127.0.0.1:5000/api/platform/setup-status" | head -c 200 || true
  echo
fi

cat <<EOF

============================================================
  KitPlatform DEPLOY XONG (can HTTPS neu Certbot OK)
============================================================

  Admin setup : https://${ADMIN_HOST}/setup
  Admin login : https://${ADMIN_HOST}/
  Customer app: https://${APP_HOST}/
  Staff POS   : https://${POS_HOST}/

  Secrets     : ${SECRETS_FILE}
  Platform key: ${PLATFORM_KEY}
  DB password : ${DB_PASS}

  Log API     : journalctl -u kit-platform-api -f
  Log SMS OTP : journalctl -u kit-platform-sms-stub -f

  Buoc tiep   : Mo /setup -> tao 1 nha thuoc + 2 chi nhanh
============================================================
EOF
