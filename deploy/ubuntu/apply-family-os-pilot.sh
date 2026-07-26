#!/usr/bin/env bash
# FamilyOS pilot deploy (incremental) — schema 192-199 + SPA family.kittech.vn + CORS.
# Does NOT rewrite api.env secrets or wipe pharmacy nginx SSL.
# Usage (on VPS after upload):
#   bash /tmp/kit-platform-upload/deploy/ubuntu/apply-family-os-pilot.sh
set -euo pipefail

UPLOAD="${UPLOAD:-/tmp/kit-platform-upload}"
WEB_ROOT="/var/www/kit-platform"
OPT="/opt/kit-platform"
CONFIG_DIR="/etc/kit-platform"
FAMILY_HOST="${FAMILY_HOST:-family.kittech.vn}"
API_PROXY_HOST="${API_PROXY_HOST:-api.novixa.vn}"
CERTBOT_EMAIL="${CERTBOT_EMAIL:-care@novixa.vn}"

log() { echo -e "\n\033[1;36m==>\033[0m $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "Chay bang root"
[[ -d "$UPLOAD/api" ]] || die "Thieu $UPLOAD/api — chay upload-to-vps.ps1 truoc"
[[ -d "$UPLOAD/migrations" ]] || die "Thieu $UPLOAD/migrations"
[[ -f "$CONFIG_DIR/api.env" ]] || die "Thieu $CONFIG_DIR/api.env"

log "Dong bo deploy scripts + migrations -> $OPT"
mkdir -p "$OPT/migrations" "$WEB_ROOT/api/uploads"
rsync -a "$UPLOAD/deploy/ubuntu/" "$OPT/"
rsync -a "$UPLOAD/migrations/" "$OPT/migrations/"
chmod +x "$OPT"/*.sh 2>/dev/null || true

log "Cap nhat API binary"
rsync -a --delete "$UPLOAD/api/" "$WEB_ROOT/api/"
# Keep uploads directory
mkdir -p "$WEB_ROOT/api/uploads"

if [[ -d "$UPLOAD/family-app" ]]; then
  log "Deploy family-app SPA"
  mkdir -p "$WEB_ROOT/family-app"
  rsync -a --delete "$UPLOAD/family-app/" "$WEB_ROOT/family-app/"
else
  die "Thieu $UPLOAD/family-app"
fi

# Optional: refresh admin so FamilyOS UI ships with same cut
if [[ -d "$UPLOAD/admin" ]]; then
  log "Cap nhat admin SPA"
  rsync -a --delete "$UPLOAD/admin/" "$WEB_ROOT/admin/"
fi

chown -R www-data:www-data "$WEB_ROOT"

log "Apply FamilyOS migrations (192-199, no demo seed)"
source "$CONFIG_DIR/secrets.generated" 2>/dev/null || true
CS=$(grep '^ConnectionStrings__Default=' "$CONFIG_DIR/api.env" | cut -d= -f2-)
DB_USER=$(echo "$CS" | sed -n 's/.*Username=\([^;]*\).*/\1/p')
DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
DB_PASS=$(echo "$CS" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
[[ -n "$DB_USER" && -n "$DB_NAME" && -n "$DB_PASS" ]] || die "Khong parse duoc ConnectionStrings__Default"
CONN="postgresql://${DB_USER}:${DB_PASS}@127.0.0.1:5432/${DB_NAME}"
bash "$OPT/run-family-os-migrations-prod.sh" "$CONN"

log "CORS — them https://${FAMILY_HOST}"
bash "$OPT/ensure-novixa-cors-env.sh" "$CONFIG_DIR/api.env"

log "Nginx site rieng cho ${FAMILY_HOST} (khong ghi de kit-platform SSL)"
mkdir -p /etc/nginx/snippets
if [[ -f "$OPT/nginx-pwa-cache.conf" ]]; then
  cp "$OPT/nginx-pwa-cache.conf" /etc/nginx/snippets/pwa-cache.conf
elif [[ -f "$UPLOAD/deploy/ubuntu/nginx-pwa-cache.conf" ]]; then
  cp "$UPLOAD/deploy/ubuntu/nginx-pwa-cache.conf" /etc/nginx/snippets/pwa-cache.conf
fi
cat > /etc/nginx/sites-available/family-kittech <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name ${FAMILY_HOST};

    root ${WEB_ROOT}/family-app;
    index index.html;
    client_max_body_size 8m;

    include /etc/nginx/snippets/pwa-cache.conf;

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host ${API_PROXY_HOST};
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 120s;
    }

    location ^~ /uploads/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Host ${API_PROXY_HOST};
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    location / {
        try_files \$uri \$uri/ /index.html;
    }

    location ~* \\.(css|png|jpg|jpeg|gif|ico|svg|woff2?)\$ {
        expires 7d;
        add_header Cache-Control "public, immutable";
    }
}
EOF
ln -sf /etc/nginx/sites-available/family-kittech /etc/nginx/sites-enabled/family-kittech
nginx -t
systemctl reload nginx

log "Restart API"
systemctl restart kit-platform-api
sleep 3
systemctl is-active --quiet kit-platform-api || {
  journalctl -u kit-platform-api -n 40 --no-pager
  die "kit-platform-api khong khoi dong"
}

if [[ "${SKIP_CERTBOT:-}" != "1" ]]; then
  log "Certbot SSL cho ${FAMILY_HOST}"
  if ! command -v certbot >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq certbot python3-certbot-nginx
  fi
  # DNS must already point family.kittech.vn -> this VPS
  if getent ahosts "$FAMILY_HOST" >/dev/null 2>&1; then
    certbot --nginx -d "$FAMILY_HOST" \
      --non-interactive --agree-tos -m "$CERTBOT_EMAIL" --redirect || \
      echo "WARN: certbot that bai — kiem tra DNS A ${FAMILY_HOST}"
  else
    echo "WARN: DNS ${FAMILY_HOST} chua resolve — bo qua certbot"
  fi
fi

log "Smoke nhanh"
curl -sf "http://127.0.0.1:5000/api/health" | head -c 200 || true
echo
curl -sf -o /dev/null -w "family HTTP %{http_code}\n" "http://${FAMILY_HOST}/" || \
  curl -sf -o /dev/null -w "family local file OK\n" -H "Host: ${FAMILY_HOST}" http://127.0.0.1/ || true

echo
echo "=== FamilyOS pilot apply xong ==="
echo "  SPA : https://${FAMILY_HOST}/"
echo "  Admin FamilyOS: https://admin.novixa.vn (tenant co module family_os)"
echo "  Schema: pack_family (192-199) — KHONG seed DEMO_FAMILY"
echo "  Tao tenant FamilyOS qua Admin /setup hoac platform provisioning"
