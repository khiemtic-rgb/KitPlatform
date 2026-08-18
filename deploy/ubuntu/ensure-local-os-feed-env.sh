#!/usr/bin/env bash
# Cho VPS đẩy feed Thái Nguyên Life (thainguyenlife.vn). Không đụng CORS / Pharmacy keys.
set -euo pipefail

ENV_FILE="${1:-/etc/kit-platform/api.env}"
[[ -f "$ENV_FILE" ]] || { echo "ERROR: missing $ENV_FILE"; exit 1; }

URL="https://thainguyenlife.vn/api/feed"
SECRET="tnl-kv-feed-7c4e91b2a8d64f0e"

tmp="$(mktemp)"
grep -v '^LocalOs__PublicFeedUrl=' "$ENV_FILE" | grep -v '^LocalOs__PublicFeedSecret=' > "$tmp" || true
printf 'LocalOs__PublicFeedUrl=%s\n' "$URL" >> "$tmp"
printf 'LocalOs__PublicFeedSecret=%s\n' "$SECRET" >> "$tmp"

cp "$ENV_FILE" "${ENV_FILE}.bak.localos.$(date +%Y%m%d%H%M%S)"
mv "$tmp" "$ENV_FILE"
chmod 600 "$ENV_FILE"
echo "Updated LocalOs feed keys in $ENV_FILE"
grep '^LocalOs__' "$ENV_FILE"
