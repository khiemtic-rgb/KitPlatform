#!/usr/bin/env bash
# Famixa GTM demo house seed — PILOT ONLY, explicit opt-in.
#
# Creates / refreshes DEMO_FAMILY (2 kids, summer + school calendar, schoolSchedule,
# viewer login `demo`) on the FamilyOS pilot database.
#
# SAFETY:
#   - NEVER called from apply-family-os-pilot.sh
#   - Requires CONFIRM=YES_SEED_FAMIXA_GTM
#   - Idempotent SQL (ON CONFLICT) — safe to re-run
#   - Does not touch pharmacy ERP data outside DEMO_FAMILY tenant rows
#
# Usage (on VPS as root, after migrations are applied):
#   CONFIRM=YES_SEED_FAMIXA_GTM bash /opt/kit-platform/seed-famixa-gtm-demo.sh
#
# From Windows (upload + run):
#   .\scripts\seed-famixa-demo-pilot.ps1 -ConfirmSeed
#
set -euo pipefail

CONFIRM="${CONFIRM:-}"
CONFIG_DIR="${CONFIG_DIR:-/etc/kit-platform}"
OPT="${OPT:-/opt/kit-platform}"
MIGRATIONS="${MIGRATIONS:-$OPT/migrations}"
UPLOAD_MIGRATIONS="${UPLOAD_MIGRATIONS:-/tmp/kit-platform-upload/migrations}"

log() { echo -e "\n\033[1;36m==>\033[0m $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ $EUID -eq 0 ]] || die "Chay bang root"
[[ "$CONFIRM" == "YES_SEED_FAMIXA_GTM" ]] || die \
  "Tu choi. Dat CONFIRM=YES_SEED_FAMIXA_GTM de seed nha demo GTM (khong chay nham vao migrate thuong)."

if [[ ! -d "$MIGRATIONS/seed" && -d "$UPLOAD_MIGRATIONS/seed" ]]; then
  MIGRATIONS="$UPLOAD_MIGRATIONS"
fi
[[ -d "$MIGRATIONS/seed" ]] || die "Thieu $MIGRATIONS/seed"
[[ -f "$CONFIG_DIR/api.env" ]] || die "Thieu $CONFIG_DIR/api.env"

CS=$(grep '^ConnectionStrings__Default=' "$CONFIG_DIR/api.env" | cut -d= -f2-)
DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
[[ -n "$DB_NAME" ]] || die "Khong parse duoc Database tu ConnectionStrings__Default"

log "Target DB: $DB_NAME (peer auth as postgres)"
echo "  Tenant : DEMO_FAMILY"
echo "  Viewer : demo  (password hash shared with local Admin@123 unless you rotate)"
echo "  Entry  : https://home.famixa.vn/demo"
echo ""
echo "  WARNING: seed nay co chu dich GTM — khong dung cho nha thuoc that."

# Ensure pack schema exists (schema-only migs; skip if already applied).
SCHEMA_FILES=(
  "192_pack_family_os.sql"
  "221_pack_family_calendar_period.sql"
  "222_pack_family_commercial_foundation.sql"
  "249_pack_family_blueprint.sql"
)
for f in "${SCHEMA_FILES[@]}"; do
  path="$MIGRATIONS/$f"
  if [[ -f "$path" ]]; then
    log "Schema ensure: $f"
    sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$path"
  else
    echo "  (skip missing $f — expect already migrated via family-os manifest)"
  fi
done

SEED_FILES=(
  "seed/006_family_os_demo.sql"
  "seed/007_family_constitution_v1.sql"
  "seed/008_family_team_siblings.sql"
  "seed/009_family_weekend_commitments.sql"
  "seed/010_family_calendar_periods.sql"
  "seed/011_family_summer_schedule_2026.sql"
  "seed/012_family_demo_gtm.sql"
)
for f in "${SEED_FILES[@]}"; do
  path="$MIGRATIONS/$f"
  [[ -f "$path" ]] || die "Missing $path"
  log "Seed: $f"
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$path"
done

if [[ -f "$MIGRATIONS/250_pack_family_app_role_grants.sql" ]]; then
  log "Re-grant pack_family app roles"
  sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -f "$MIGRATIONS/250_pack_family_app_role_grants.sql"
fi

log "Verify"
sudo -u postgres psql -d "$DB_NAME" -t -A <<'SQL'
SELECT
  'tenant=' || t.tenant_code
  || ' demoHouse=' || COALESCE(t.settings#>>'{platform,features,demoHouse}', 'false')
FROM public.tenants t
WHERE t.tenant_code = 'DEMO_FAMILY' AND t.deleted_at IS NULL;
SELECT
  'members=' || COUNT(*)::text
FROM pack_family.membership m
WHERE m.family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01' AND m.deleted_at IS NULL;
SELECT
  'viewer_user=' || u.username || ' role=' || m.role_code
FROM public.users u
JOIN pack_family.membership m ON m.user_id = u.id AND m.deleted_at IS NULL
WHERE u.username = 'demo'
  AND u.tenant_id = '11111111-1111-1111-1111-111111111104'
  AND u.deleted_at IS NULL;
SELECT
  'school_kids=' || COUNT(*)::text
FROM pack_family.family_blueprint b,
  LATERAL jsonb_object_keys(COALESCE(b.layers_json->'members','{}'::jsonb)) k
WHERE b.family_id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01'
  AND (b.layers_json->'members'->k ? 'schoolSchedule');
SQL

echo ""
echo "=== Famixa GTM seed OK ==="
echo "  SPA URL : https://home.famixa.vn/demo"
echo "  Alt     : https://family.kittech.vn/demo"
echo "  Login   : DEMO_FAMILY / demo / (local default Admin@123 unless rotated)"
echo "  Admin   : DEMO_FAMILY / admin — GIU RIENG, khong share cong dong"
echo ""
echo "Smoke:"
echo "  curl -sS -X POST https://api.novixa.vn/api/auth/login \\"
echo "    -H 'Content-Type: application/json' \\"
echo "    -d '{\"tenantCode\":\"DEMO_FAMILY\",\"username\":\"demo\",\"password\":\"Admin@123\"}'"
