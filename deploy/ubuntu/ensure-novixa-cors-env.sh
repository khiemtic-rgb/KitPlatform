#!/usr/bin/env bash
# Ensure /etc/kit-platform/api.env lists every Novixa SPA origin.
# ASP.NET Core env arrays REPLACE appsettings — a partial list drops missing origins.
set -euo pipefail

ENV_FILE="${1:-/etc/kit-platform/api.env}"
[[ -f "$ENV_FILE" ]] || { echo "ERROR: missing $ENV_FILE"; exit 1; }

REQUIRED=(
  "https://admin.novixa.vn"
  "https://app.novixa.vn"
  "https://pos.novixa.vn"
  "https://survey.novixa.vn"
  "https://prescriber.novixa.vn"
  "https://partner.novixa.vn"
)

tmp="$(mktemp)"
# Drop existing Cors__AllowedOrigins__* lines, keep everything else
grep -v '^Cors__AllowedOrigins__' "$ENV_FILE" > "$tmp" || true

i=0
for origin in "${REQUIRED[@]}"; do
  echo "Cors__AllowedOrigins__${i}=${origin}" >> "$tmp"
  i=$((i + 1))
done

cp "$ENV_FILE" "${ENV_FILE}.bak.cors.$(date +%Y%m%d%H%M%S)"
mv "$tmp" "$ENV_FILE"
chmod 600 "$ENV_FILE"
echo "Updated CORS origins in $ENV_FILE:"
grep '^Cors__AllowedOrigins__' "$ENV_FILE"
