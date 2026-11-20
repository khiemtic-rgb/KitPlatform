#!/usr/bin/env bash
# Stage the production credential data-encryption key for an immediate pull.
# The destination machine is not an SSH server. This file is not an off-machine backup.
# PRODUCTION_CREDENTIAL_KEY_PULL_STAGED means the pull user can read the file now.
# It does not mean PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN and it does not prove a restore.
# Does not print the key. Does not generate a key. Does not write api.env. Does not write pg_dump.
# Do not run this script under bash -x.
# Required before staging, each exactly true:
#   OFFMACHINE_CONFIRMED
#   PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED
#   PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED
# Required: PULL_USER, the account that will pull the file and then delete it.
# Production directory is only /run/kit-production-credential-pull.
set -euo pipefail
set +x

ENV_FILE="${ENV_FILE:-/etc/kit-platform/api.env}"
PULL_USER="${PULL_USER:-}"
STAGE_DIR="${STAGE_DIR:-/run/kit-production-credential-pull}"
FIXTURE="${KIT_CREDENTIAL_PULL_FIXTURE:-}"

fail() {
  printf '%s\n' "$1"
  exit 1
}

require_true() {
  local value="$1"
  if [[ "$value" != "true" ]]; then
    fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
  fi
}

require_true "${OFFMACHINE_CONFIRMED:-}"
require_true "${PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED:-}"
require_true "${PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED:-}"

if [[ ! "$PULL_USER" =~ ^[A-Za-z0-9._-]+$ ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if [[ "$FIXTURE" == "true" ]]; then
  if [[ "$ENV_FILE" == "/etc/kit-platform/api.env" ]]; then
    fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
  fi
  case "$STAGE_DIR" in
    /tmp/kit-credential-pull-fixture-[A-Za-z0-9._-]*) ;;
    *) fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED ;;
  esac
elif [[ -n "$FIXTURE" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
elif [[ "$STAGE_DIR" != "/run/kit-production-credential-pull" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

case "$STAGE_DIR" in
  /etc/kit-platform|/etc/kit-platform/*|/var/backups|/var/backups/*)
    fail PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE
    ;;
esac

if [[ "$STAGE_DIR" == "$(dirname "$ENV_FILE")" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE
fi

if ! command -v openssl >/dev/null 2>&1; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if ! id -u "$PULL_USER" >/dev/null 2>&1; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if [[ ! -f "$ENV_FILE" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_MISSING
fi

if [[ -d "$STAGE_DIR" ]]; then
  if find "$STAGE_DIR" -mindepth 1 -maxdepth 1 \( -name 'key-*.env' -o -name '.staging.*' \) -print -quit | grep -q .; then
    fail PRODUCTION_CREDENTIAL_KEY_PULL_STAGE_EXISTS
  fi
fi

key_id="$(grep -E '^ProductionCredentials__DataEncryptionKeyId=' "$ENV_FILE" | head -n1 | cut -d= -f2- | tr -d '\r' || true)"
key_line="$(grep -E '^ProductionCredentials__DataEncryptionKey=' "$ENV_FILE" | head -n1 | tr -d '\r' || true)"
key_count="$(grep -c -E '^ProductionCredentials__DataEncryptionKey=' "$ENV_FILE" || true)"
id_count="$(grep -c -E '^ProductionCredentials__DataEncryptionKeyId=' "$ENV_FILE" || true)"

if [[ "$key_count" != "1" || "$id_count" != "1" || -z "$key_line" || -z "$key_id" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_MISSING
fi

if [[ "$key_id" =~ [[:space:][:cntrl:]] ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_INVALID
fi

# The source may be api.env. Only the two lines above are written. Any other line stays out of the staged file.
key_b64="${key_line#ProductionCredentials__DataEncryptionKey=}"
size="$(printf '%s' "$key_b64" | openssl base64 -d -A 2>/dev/null | wc -c | tr -d '[:space:]' || true)"
if [[ "$size" != "32" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_INVALID
fi

if ! mkdir -p "$STAGE_DIR"; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi
chmod 700 "$STAGE_DIR"

name="key-$(openssl rand -hex 16).env"
dest="$STAGE_DIR/$name"
umask 077
tmp="$(mktemp "$STAGE_DIR/.staging.XXXXXX")"
cleanup_tmp() {
  rm -f "$tmp"
}
trap cleanup_tmp EXIT

{
  printf '%s\n' "ProductionCredentials__DataEncryptionKeyId=${key_id}"
  printf '%s\n' "$key_line"
} > "$tmp"

current="$(id -un)"
if [[ "$current" != "$PULL_USER" ]]; then
  if ! chown "$PULL_USER" "$STAGE_DIR" "$tmp"; then
    rm -f "$tmp"
    fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
  fi
fi

chmod 400 "$tmp"
mv "$tmp" "$dest"
trap - EXIT
chmod 400 "$dest"

if [[ "$FIXTURE" == "true" ]]; then
  printf '%s\n' "PRODUCTION_CREDENTIAL_KEY_PULL_FIXTURE_STAGED"
else
  printf '%s\n' "PRODUCTION_CREDENTIAL_KEY_PULL_STAGED"
fi
printf '%s\n' "PULL_PATH=${dest}"
