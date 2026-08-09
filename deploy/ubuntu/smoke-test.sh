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

# Feature routes exist (401/403 = mounted; 404 = missing deploy)
check_api_route() {
  local path="$1"
  local label="$2"
  echo -n "$label ... "
  local code
  code="$(curl -s -o /dev/null -w '%{http_code}' "$API$path" || true)"
  if [[ "$code" == "401" || "$code" == "403" || "$code" == "200" ]]; then
    echo "OK ($code)"
  else
    echo "FAIL (HTTP $code — expected 401/403/200)"
    exit 1
  fi
}

check_api_route "/api/procurement/supplier-payables" "API supplier-payables route"
check_api_route "/api/sales/customer-receivables" "API customer-receivables route"
check_api_route "/api/inventory/transfers" "API inventory transfers route"

echo -n "Migration manifest 279/280 ... "
MANIFEST="/opt/kit-platform/migration-files.prod.txt"
if [[ -f "$MANIFEST" ]] \
  && grep -q '279_inventory_count_allow_zero_qty.sql' "$MANIFEST" \
  && grep -q '280_inventory_transfer_ship_receive.sql' "$MANIFEST"; then
  echo "OK"
elif [[ ! -f "$MANIFEST" ]]; then
  echo "SKIP (manifest not on host)"
else
  echo "FAIL (279/280 missing in $MANIFEST)"
  exit 1
fi

echo "=== All checks passed ==="
