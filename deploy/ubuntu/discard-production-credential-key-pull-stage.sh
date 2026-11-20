#!/usr/bin/env bash
# Delete one staged pull file. Does not print file contents.
# Production path: /run/kit-production-credential-pull/key-<32 hex>.env
# A fixture path requires KIT_CREDENTIAL_PULL_FIXTURE=true.
set -euo pipefail
set +x

target="${1:-}"
fixture="${KIT_CREDENTIAL_PULL_FIXTURE:-}"

fail() {
  printf '%s\n' "$1"
  exit 1
}

if [[ -z "$target" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

case "$target" in
  /etc/kit-platform|/etc/kit-platform/*|/var/backups|/var/backups/*)
    fail PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE
    ;;
esac

base="$(basename "$target")"
dir="$(dirname "$target")"

if [[ ! "$base" =~ ^key-[0-9a-f]{32}\.env$ ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if [[ "$fixture" == "true" ]]; then
  case "$dir" in
    /tmp/kit-credential-pull-fixture-[A-Za-z0-9._-]*) ;;
    *) fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED ;;
  esac
elif [[ -n "$fixture" || "$dir" != "/run/kit-production-credential-pull" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if [[ ! -f "$target" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_MISSING
fi

rm -f -- "$target"
if [[ -f "$target" ]]; then
  fail PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED
fi

if [[ "$fixture" == "true" ]]; then
  rmdir "$dir" 2>/dev/null || true
fi

printf '%s\n' "PRODUCTION_CREDENTIAL_KEY_PULL_STAGE_DISCARDED"
