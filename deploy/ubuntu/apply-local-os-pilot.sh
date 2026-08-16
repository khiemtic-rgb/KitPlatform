#!/usr/bin/env bash
# Apply KIT Local OS (Thái Nguyên Life) migrations only.
# No API/admin deploy. No Pharmacy / Family / Content manifests. No service restart.
# Usage (on VPS after upload):
#   bash /tmp/kit-local-os/apply-local-os-pilot.sh
set -euo pipefail

UPLOAD="${UPLOAD:-/tmp/kit-local-os}"
CONFIG_DIR="/etc/kit-platform"

log() { echo -e "\n\033[1;36m==>\033[0m $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "Chay bang root"
[[ -f "$CONFIG_DIR/api.env" ]] || die "Thieu $CONFIG_DIR/api.env"
[[ -f "$UPLOAD/migration-files.local-os.txt" ]] || die "Thieu $UPLOAD/migration-files.local-os.txt"
[[ -d "$UPLOAD/migrations" ]] || die "Thieu $UPLOAD/migrations"

CS=$(grep '^ConnectionStrings__Default=' "$CONFIG_DIR/api.env" | cut -d= -f2-)
DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
[[ -n "$DB_NAME" ]] || die "Khong parse duoc Database tu ConnectionStrings__Default"

log "Local OS migs only — DB=$DB_NAME (peer auth, no api.env password)"

# Refuse if someone pointed this at a Pharmacy manifest by mistake.
if grep -qiE 'pack_pharmacy|DEMO_PHARMACY|NT_XUANHOA' "$UPLOAD/migration-files.local-os.txt"; then
  die "Manifest local-os khong hop le — co ve da tron Pharmacy"
fi

LIST="$UPLOAD/migration-files.local-os.txt"
MIGRATIONS="$UPLOAD/migrations"
while IFS= read -r line || [[ -n "$line" ]]; do
  line="${line//$'\r'/}"
  file="$(echo "$line" | sed 's/#.*//' | xargs || true)"
  [[ -z "$file" ]] && continue
  path="$MIGRATIONS/$file"
  [[ -f "$path" ]] || die "Missing $path"
  echo ">> $file"
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$path"
done < "$LIST"

log "Grant pack_local -> pharmacore/kitplatform (peer-auth tables)"
sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 <<'SQL'
DO $$
DECLARE
  r text;
BEGIN
  FOREACH r IN ARRAY ARRAY['pharmacore', 'kitplatform'] LOOP
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = r) THEN
      EXECUTE format('GRANT USAGE, CREATE ON SCHEMA pack_local TO %I', r);
      EXECUTE format('GRANT ALL ON ALL TABLES IN SCHEMA pack_local TO %I', r);
      EXECUTE format('GRANT ALL ON ALL SEQUENCES IN SCHEMA pack_local TO %I', r);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_local GRANT ALL ON TABLES TO %I',
        r);
      EXECUTE format(
        'ALTER DEFAULT PRIVILEGES FOR ROLE postgres IN SCHEMA pack_local GRANT ALL ON SEQUENCES TO %I',
        r);
    END IF;
  END LOOP;
END $$;
SQL

log "Smoke tenant — KIT_LOCAL plus Pharmacy tenants still present"
sudo -u postgres psql -d "$DB_NAME" -c "
SELECT tenant_code, status, deleted_at IS NOT NULL AS deleted
FROM public.tenants
WHERE tenant_code IN ('KIT_LOCAL', 'DEMO_PHARMACY', 'NT_XUANHOA')
ORDER BY tenant_code;
"
sudo -u postgres psql -d "$DB_NAME" -t -A -c "
SELECT 'pack_local_tables=' || count(*)
FROM information_schema.tables
WHERE table_schema='pack_local';
"
sudo -u postgres psql -d "$DB_NAME" -t -A -c "
SELECT 'kit_local_admin=' || count(*)
FROM public.users u
JOIN public.tenants t ON t.id = u.tenant_id
WHERE t.tenant_code = 'KIT_LOCAL' AND u.username = 'admin' AND u.status = 1 AND u.deleted_at IS NULL;
"

echo "=== Local OS pilot DB apply done (API not restarted) ==="
