#!/usr/bin/env bash
# Apply Content Park migrations on a VPS that already has KitPlatform.
# Does not touch Pharmacy prod runner or wipe DB.
# Usage (on VPS): sudo bash /opt/kit-platform/apply-kit-migrations-content.sh
set -euo pipefail

OPT="${OPT:-/opt/kit-platform}"
CONFIG_DIR="${CONFIG_DIR:-/etc/kit-platform}"

die() { echo "ERROR: $*" >&2; exit 1; }
[[ $EUID -eq 0 ]] || die "Chay bang root"
[[ -f "$OPT/run-kit-migrations-content.sh" ]] || die "Thieu $OPT/run-kit-migrations-content.sh"
[[ -f "$CONFIG_DIR/api.env" ]] || die "Thieu $CONFIG_DIR/api.env"

CS=$(grep '^ConnectionStrings__Default=' "$CONFIG_DIR/api.env" | cut -d= -f2-)
[[ -n "$CS" ]] || die "Khong doc duoc ConnectionStrings__Default"

# Prefer sudo -u postgres when URI password is messy.
if command -v sudo >/dev/null && id postgres >/dev/null 2>&1; then
  DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
  [[ -n "$DB_NAME" ]] || die "Khong parse Database"
  echo "DB=$DB_NAME (sudo -u postgres)"
  sudo -u postgres bash "$OPT/run-kit-migrations-content.sh" "postgresql://postgres@127.0.0.1:5432/${DB_NAME}"
else
  DB_USER=$(echo "$CS" | sed -n 's/.*Username=\([^;]*\).*/\1/p')
  DB_NAME=$(echo "$CS" | sed -n 's/.*Database=\([^;]*\).*/\1/p')
  DB_PASS=$(echo "$CS" | sed -n 's/.*Password=\([^;]*\).*/\1/p')
  CONN="postgresql://${DB_USER}:${DB_PASS}@127.0.0.1:5432/${DB_NAME}"
  echo "DB=$DB_NAME USER=$DB_USER"
  bash "$OPT/run-kit-migrations-content.sh" "$CONN"
fi
