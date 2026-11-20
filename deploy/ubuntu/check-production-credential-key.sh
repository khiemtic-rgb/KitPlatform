#!/usr/bin/env bash
# Fail closed when the Production credential data-encryption key is missing or the wrong size.
# Does not print the key. Does not generate a key. A database dump cannot replace this key.
# Optional restore comparison: set RESTORE_ENV_FILE to a second copy.
# The comparison prints PRODUCTION_CREDENTIAL_KEY_RESTORE_OK or PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH.
# Running this script in a repository checkout is not evidence that a restore was performed.
set -euo pipefail
set +x

ENV_FILE="${ENV_FILE:-/etc/kit-platform/api.env}"
RESTORE_ENV_FILE="${RESTORE_ENV_FILE:-}"

load_key() {
  local file="$1"
  local id_var="$2"
  local b64_var="$3"
  if [[ ! -f "$file" ]]; then
    echo "PRODUCTION_CREDENTIAL_KEY_MISSING"
    exit 1
  fi
  local id b64
  id="$(grep -E '^ProductionCredentials__DataEncryptionKeyId=' "$file" | head -n1 | cut -d= -f2- || true)"
  b64="$(grep -E '^ProductionCredentials__DataEncryptionKey=' "$file" | head -n1 | cut -d= -f2- || true)"
  if [[ -z "${id}" || -z "${b64}" ]]; then
    echo "PRODUCTION_CREDENTIAL_KEY_MISSING"
    exit 1
  fi
  printf -v "$id_var" '%s' "$id"
  printf -v "$b64_var" '%s' "$b64"
}

key_size() {
  printf '%s' "$1" | openssl base64 -d -A 2>/dev/null | wc -c | tr -d '[:space:]' || true
}

load_key "$ENV_FILE" key_id key_b64
size="$(key_size "$key_b64")"
if [[ "$size" != "32" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_INVALID"
  exit 1
fi

if [[ -z "$RESTORE_ENV_FILE" ]]; then
  echo "PRODUCTION_CREDENTIAL_KEY_OK"
else
  load_key "$RESTORE_ENV_FILE" restore_id restore_b64
  restore_size="$(key_size "$restore_b64")"
  if [[ "$restore_size" != "32" ]]; then
    echo "PRODUCTION_CREDENTIAL_KEY_INVALID"
    exit 1
  fi
  if [[ "$key_id" != "$restore_id" ]]; then
    echo "PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH"
    exit 1
  fi
  if ! cmp -s <(printf '%s' "$key_b64" | openssl base64 -d -A) <(printf '%s' "$restore_b64" | openssl base64 -d -A); then
    echo "PRODUCTION_CREDENTIAL_KEY_RESTORE_MISMATCH"
    exit 1
  fi
  echo "PRODUCTION_CREDENTIAL_KEY_RESTORE_OK"
fi
