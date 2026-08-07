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
# Public brand host must stay on the same vhost + cert (SAN), or phones show ERR_CERT_COMMON_NAME_INVALID.
FAMILY_ALT_HOST="${FAMILY_ALT_HOST:-home.famixa.vn}"
FAMILY_HOSTS="${FAMILY_HOSTS:-$FAMILY_ALT_HOST $FAMILY_HOST}"
API_PROXY_HOST="${API_PROXY_HOST:-api.novixa.vn}"
CERTBOT_EMAIL="${CERTBOT_EMAIL:-care@novixa.vn}"
FAMILY_CERT_DIR="${FAMILY_CERT_DIR:-/etc/letsencrypt/live/family.kittech.vn}"

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

log "Apply FamilyOS migrations (manifest family-os.txt, no demo seed)"
# Prefer postgres peer auth — avoids api.env password quoting / URI-reserved chars.
CS=$(grep '^ConnectionStrings__Default=' "$CONFIG_DIR/api.env" | cut -d= -f2-)
DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
[[ -n "$DB_NAME" ]] || die "Khong parse duoc Database tu ConnectionStrings__Default"
LIST="$OPT/migration-files.family-os.txt"
[[ -f "$LIST" ]] || die "Thieu $LIST"
MIGRATIONS="$OPT/migrations"
while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line//$'\r'/}"
  file="$(echo "$line" | sed 's/#.*//' | xargs || true)"
  [[ -z "$file" ]] && continue
  path="$MIGRATIONS/$file"
  [[ -f "$path" ]] || die "Missing $path"
  echo ">> $file"
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$path"
done < "$LIST"
tables=$(sudo -u postgres psql -d "$DB_NAME" -t -A -c "SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_family'")
echo "=== Done: pack_family tables=$tables ==="
# Peer-auth migs run as postgres — re-grant app roles so new tables are readable.
if [[ -f "$MIGRATIONS/250_pack_family_app_role_grants.sql" ]]; then
  log "Grant pack_family -> pharmacore/kitplatform"
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$MIGRATIONS/250_pack_family_app_role_grants.sql"
fi

log "CORS — them https://${FAMILY_HOST}"
bash "$OPT/ensure-novixa-cors-env.sh" "$CONFIG_DIR/api.env"

log "Nginx site rieng cho ${FAMILY_HOSTS} (khong ghi de kit-platform SSL)"
mkdir -p /etc/nginx/snippets
if [[ -f "$OPT/nginx-pwa-cache.conf" ]]; then
  cp "$OPT/nginx-pwa-cache.conf" /etc/nginx/snippets/pwa-cache.conf
elif [[ -f "$UPLOAD/deploy/ubuntu/nginx-pwa-cache.conf" ]]; then
  cp "$UPLOAD/deploy/ubuntu/nginx-pwa-cache.conf" /etc/nginx/snippets/pwa-cache.conf
fi

FAMILY_LOCATIONS=$(cat <<EOF
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
        # Do NOT use try_files with a trailing-slash URI form — real asset dirs
        # (e.g. former /unlock/) become 403.
        try_files \$uri /index.html;
    }

    # Brand icons keep stable filenames, so they must revalidate instead of
    # being pinned by the immutable rule below (phones kept the old logo).
    location ~* ^/(favicon[^/]*|apple-touch-icon[^/]*|icon-[0-9]+\\.png|icon\\.svg)\$ {
        add_header Cache-Control "no-cache, must-revalidate";
    }

    location ~* \\.(css|png|jpg|jpeg|gif|ico|svg|woff2?)\$ {
        expires 7d;
        add_header Cache-Control "public, immutable";
    }
EOF
)

if [[ -f "$FAMILY_CERT_DIR/fullchain.pem" && -f "$FAMILY_CERT_DIR/privkey.pem" ]]; then
  # Prefer the dual-SAN cert. Never let certbot --nginx -d \$FAMILY_HOST alone
  # create a single-name cert that breaks home.famixa.vn.
  cat > /etc/nginx/sites-available/family-kittech <<EOF
server {
    server_name ${FAMILY_HOSTS};

${FAMILY_LOCATIONS}

    listen [::]:443 ssl;
    listen 443 ssl;
    ssl_certificate ${FAMILY_CERT_DIR}/fullchain.pem;
    ssl_certificate_key ${FAMILY_CERT_DIR}/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;
}

server {
    if (\$host = ${FAMILY_HOST}) {
        return 301 https://\$host\$request_uri;
    }
    if (\$host = ${FAMILY_ALT_HOST}) {
        return 301 https://\$host\$request_uri;
    }

    listen 80;
    listen [::]:80;
    server_name ${FAMILY_HOSTS};
    return 404;
}
EOF
else
  cat > /etc/nginx/sites-available/family-kittech <<EOF
server {
    listen 80;
    listen [::]:80;
    server_name ${FAMILY_HOSTS};

${FAMILY_LOCATIONS}
}
EOF
fi

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

if [[ "${SKIP_CERTBOT:-}" != "1" && ! -f "$FAMILY_CERT_DIR/fullchain.pem" ]]; then
  log "Certbot SSL cho ${FAMILY_HOSTS}"
  if ! command -v certbot >/dev/null 2>&1; then
    export DEBIAN_FRONTEND=noninteractive
    apt-get update -qq
    apt-get install -y -qq certbot python3-certbot-nginx
  fi
  cert_args=()
  for h in $FAMILY_HOSTS; do
    if getent ahosts "$h" >/dev/null 2>&1; then
      cert_args+=(-d "$h")
    else
      echo "WARN: DNS $h chua resolve — bo qua host nay"
    fi
  done
  if ((${#cert_args[@]} > 0)); then
    certbot --nginx "${cert_args[@]}" \
      --cert-name family.kittech.vn \
      --non-interactive --agree-tos -m "$CERTBOT_EMAIL" --redirect || \
      echo "WARN: certbot that bai — kiem tra DNS A ${FAMILY_HOSTS}"
  else
    echo "WARN: Khong host Family nao resolve — bo qua certbot"
  fi
else
  log "SSL — dung cert san co ${FAMILY_CERT_DIR} (SAN: ${FAMILY_HOSTS})"
fi

log "Smoke nhanh"
curl -sf "http://127.0.0.1:5000/api/health" | head -c 200 || true
echo
curl -sf -o /dev/null -w "family HTTPS %{http_code}\n" "https://${FAMILY_HOST}/" || true
curl -sf -o /dev/null -w "famixa HTTPS %{http_code}\n" "https://${FAMILY_ALT_HOST}/" || true

echo
echo "=== FamilyOS pilot apply xong ==="
echo "  SPA : https://${FAMILY_HOST}/  |  https://${FAMILY_ALT_HOST}/"
echo "  Admin FamilyOS: https://admin.novixa.vn (tenant co module family_os)"
echo "  Schema: pack_family (192-199) — KHONG seed DEMO_FAMILY"
echo "  Tao tenant FamilyOS qua Admin /setup hoac platform provisioning"
