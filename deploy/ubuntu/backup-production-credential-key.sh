#!/usr/bin/env bash
# Copy the Production credential data-encryption key to an explicit remote destination.
# A different directory on this machine is not an off-machine backup.
# Source: /etc/kit-platform/api.env
# Names only: ProductionCredentials__DataEncryptionKey and ProductionCredentials__DataEncryptionKeyId
# Does not print the key. Does not generate a key. Does not write the key into pg_dump.
# Does not prove that the copy was restored. PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN means the remote copy command finished.
# Required before the copy, each exactly true:
#   OFFMACHINE_CONFIRMED
#   PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED
#   PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED
# Destination: OFFMACHINE_DEST=user@host:/absolute/path
# host must not be localhost, 127.0.0.1, ::1, or this machine's short hostname.
set -euo pipefail
set +x

ENV_FILE="${ENV_FILE:-/etc/kit-platform/api.env}"
DEST="${OFFMACHINE_DEST:-}"

require_true() {
  local name="$1"
  local value="$2"
  if [[ "$value" != "true" ]]; then
    echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED"
    exit 1
  fi
}

require_true OFFMACHINE_CONFIRMED "${OFFMACHINE_CONFIRMED:-}"
require_true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED "${PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED:-}"
require_true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED "${PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED:-}"

if [[ -z "$DEST" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED"
  exit 1
fi

# Local paths are same-machine even when they are outside the configuration directory.
case "$DEST" in
  /*|./*|../*|~/*|[A-Za-z]:*)
    echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE"
    exit 1
    ;;
esac

if [[ ! "$DEST" =~ ^[A-Za-z0-9._-]+@[A-Za-z0-9._-]+:/[^[:space:]]+$ ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE"
  exit 1
fi

host="${DEST#*@}"
host="${host%%:*}"
self="$(hostname -s 2>/dev/null || hostname 2>/dev/null || true)"
case "$host" in
  localhost|127.0.0.1|::1|"$self")
    echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE"
    exit 1
    ;;
esac

env_dir="$(dirname "$ENV_FILE")"
dest_path="${DEST#*:}"
case "$dest_path" in
  /etc/kit-platform|/etc/kit-platform/*|/var/backups|/var/backups/*)
    echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE"
    exit 1
    ;;
esac
if [[ "$dest_path" == "$ENV_FILE" || "$(dirname "$dest_path")" == "$env_dir" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE"
  exit 1
fi

if [[ ! -f "$ENV_FILE" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_MISSING"
  exit 1
fi

key_id="$(grep -E '^ProductionCredentials__DataEncryptionKeyId=' "$ENV_FILE" | head -n1 | cut -d= -f2- || true)"
key_line="$(grep -E '^ProductionCredentials__DataEncryptionKey=' "$ENV_FILE" | head -n1 || true)"

if [[ -z "${key_line}" || -z "${key_id}" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_MISSING"
  exit 1
fi

tmp="$(mktemp)"
cleanup() {
  rm -f "$tmp"
}
trap cleanup EXIT
umask 077
{
  printf '%s\n' "ProductionCredentials__DataEncryptionKeyId=${key_id}"
  printf '%s\n' "${key_line}"
} > "$tmp"
chmod 600 "$tmp"
if ! scp -q -o BatchMode=yes -o StrictHostKeyChecking=yes "$tmp" "$DEST"; then
  echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED"
  exit 1
fi
echo "PRODUCTION_CREDENTIAL_KEY_BACKUP_WRITTEN"
