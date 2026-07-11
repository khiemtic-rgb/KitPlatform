#!/usr/bin/env bash
# Quick smoke test after deploy
set -euo pipefail

API="${API_BASE:-https://api.novixa.vn}"
ADMIN="${ADMIN_BASE:-https://admin.novixa.vn}"
APP="${APP_BASE:-https://app.novixa.vn}"
POS="${POS_BASE:-https://pos.novixa.vn}"
SURVEY="${SURVEY_BASE:-https://survey.novixa.vn}"
PRESCRIBER="${PRESCRIBER_BASE:-https://prescriber.novixa.vn}"
PARTNER="${PARTNER_BASE:-https://partner.novixa.vn}"

echo "=== KitPlatform smoke test ==="

echo -n "API setup-status ... "
curl -sf "$API/api/platform/setup-status" | grep -q '"tenantsCount"' && echo OK || { echo FAIL; exit 1; }

echo -n "Admin index.html ... "
curl -sf "$ADMIN/" | grep -q '<html' && echo OK || { echo FAIL; exit 1; }

echo -n "Customer app index.html ... "
curl -sf "$APP/" | grep -q '<html' && echo OK || { echo FAIL; exit 1; }

echo -n "Staff POS index.html ... "
curl -sf "$POS/" | grep -q '<html' && echo OK || { echo FAIL; exit 1; }

echo -n "Staff POS manifest ... "
curl -sf "$POS/manifest.webmanifest" | grep -q 'Novixa' && echo OK || { echo FAIL; exit 1; }

echo -n "API health (DB) ... "
if curl -sf "$API/api/health/db" | grep -q '"database":true'; then
  echo OK
else
  echo FAIL
  exit 1
fi

check_cors() {
  local origin="$1"
  local host
  host="$(echo "$origin" | sed -E 's#https://##')"
  echo -n "CORS $host ... "
  if curl -sf -D - -o /dev/null -H "Origin: $origin" "$API/api/health" \
    | grep -qi "Access-Control-Allow-Origin: $origin"; then
    echo OK
  else
    echo FAIL
    echo "  Hint: run deploy/ubuntu/ensure-novixa-cors-env.sh then restart kit-platform-api"
    echo "  (Cors__AllowedOrigins__* in api.env replaces appsettings — must list all SPAs)."
    exit 1
  fi
}

check_cors "https://admin.novixa.vn"
check_cors "https://app.novixa.vn"
check_cors "https://pos.novixa.vn"
check_cors "https://survey.novixa.vn"
check_cors "https://prescriber.novixa.vn"
check_cors "https://partner.novixa.vn"

echo "=== All checks passed ==="
