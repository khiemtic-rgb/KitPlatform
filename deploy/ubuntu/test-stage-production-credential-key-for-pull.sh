#!/usr/bin/env bash
# Fixture checks for the pull-stage scripts. Does not read or write /etc/kit-platform/api.env.
# Does not print key material. A failed leak check prints FIXTURE_OUTPUT_LEAKED and not the key.
set -euo pipefail
set +x

if ! command -v openssl >/dev/null 2>&1; then
  export PATH="/usr/bin:$PATH"
fi

here="$(cd "$(dirname "$0")" && pwd)"
stage="$here/stage-production-credential-key-for-pull.sh"
discard="$here/discard-production-credential-key-pull-stage.sh"
work="$(mktemp -d /tmp/kit-credential-pull-test-XXXXXX)"
stage_dirs=()

cleanup() {
  rm -rf "$work"
  local dir
  for dir in "${stage_dirs[@]+"${stage_dirs[@]}"}"; do
    rm -rf "$dir"
  done
}
trap cleanup EXIT

pull_user="$(id -un)"
key_b64="$(openssl rand -base64 32 | tr -d '\n')"
key_bytes="$(printf '%s' "$key_b64" | openssl base64 -d -A | wc -c | tr -d '[:space:]')"
if [[ "$key_bytes" != "32" ]]; then
  printf '%s\n' "FIXTURE_KEY_BYTES=${key_bytes}"
  exit 1
fi
printf '%s\n' "FIXTURE_KEY_BYTES=32"

env_file="$work/fixture.env"
{
  printf '%s\n' "ProductionCredentials__DataEncryptionKeyId=fixture-not-production"
  printf '%s\n' "ProductionCredentials__DataEncryptionKey=${key_b64}"
} > "$env_file"
chmod 600 "$env_file"

assert_token() {
  local expected="$1"
  local actual="$2"
  if [[ "$actual" != "$expected" ]]; then
    printf '%s\n' "FIXTURE_TOKEN_MISMATCH expected=${expected} actual=${actual}"
    if [[ -f "$work/err.txt" ]] && ! grep -F "$key_b64" "$work/err.txt" >/dev/null 2>&1 && ! grep -F "DataEncryptionKey=" "$work/err.txt" >/dev/null 2>&1; then
      printf '%s\n' "FIXTURE_STDERR_BEGIN"
      cat "$work/err.txt"
      printf '%s\n' "FIXTURE_STDERR_END"
    fi
    exit 1
  fi
}

assert_no_leak() {
  local file="$1"
  if grep -F "$key_b64" "$file" >/dev/null 2>&1; then
    printf '%s\n' "FIXTURE_OUTPUT_LEAKED"
    exit 1
  fi
  if grep -F "DataEncryptionKey=" "$file" >/dev/null 2>&1; then
    printf '%s\n' "FIXTURE_OUTPUT_LEAKED"
    exit 1
  fi
}

run_stage() {
  trap - EXIT
  local out="$work/out.txt"
  local err="$work/err.txt"
  set +e
  "$@" >"$out" 2>"$err"
  local code=$?
  set -e
  assert_no_leak "$out"
  assert_no_leak "$err"
  printf '%s\n' "$code"
}

# Missing evidence tokens.
mapfile -t missing < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$env_file" KIT_CREDENTIAL_PULL_FIXTURE=true STAGE_DIR="/tmp/kit-credential-pull-fixture-missing" bash "$stage")
assert_token "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED" "$(head -n1 "$work/out.txt")"
assert_token "1" "${missing[0]}"
printf '%s\n' "FIXTURE_MISSING_TOKENS_REJECTED"

# Fixture mode must not touch the production env path.
mapfile -t prod_guard < <(run_stage env PULL_USER="$pull_user" ENV_FILE="/etc/kit-platform/api.env" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="/tmp/kit-credential-pull-fixture-prodguard" bash "$stage")
assert_token "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED" "$(head -n1 "$work/out.txt")"
assert_token "1" "${prod_guard[0]}"
printf '%s\n' "FIXTURE_PRODUCTION_ENV_REJECTED"

# A directory outside the fixture prefix is refused.
mapfile -t bad_dir < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$env_file" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="/tmp/not-the-fixture-prefix" bash "$stage")
assert_token "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED" "$(head -n1 "$work/out.txt")"
assert_token "1" "${bad_dir[0]}"
printf '%s\n' "FIXTURE_STAGE_DIR_REJECTED"

# Wrong key size.
short_file="$work/short.env"
{
  printf '%s\n' "ProductionCredentials__DataEncryptionKeyId=fixture-not-production"
  printf '%s\n' "ProductionCredentials__DataEncryptionKey=YQ=="
} > "$short_file"
short_dir="/tmp/kit-credential-pull-fixture-short"
stage_dirs+=("$short_dir")
mapfile -t short_run < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$short_file" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="$short_dir" bash "$stage")
assert_token "PRODUCTION_CREDENTIAL_KEY_INVALID" "$(head -n1 "$work/out.txt")"
assert_token "1" "${short_run[0]}"
if find "$short_dir" -name 'key-*.env' -print -quit 2>/dev/null | grep -q .; then
  printf '%s\n' "FIXTURE_SHORT_KEY_LEFT_FILE"
  exit 1
fi
printf '%s\n' "FIXTURE_SHORT_KEY_REJECTED"

# A larger env file must stage only the two key lines.
dump_file="$work/dump.env"
{
  printf '%s\n' "UNRELATED=present"
  printf '%s\n' "ProductionCredentials__DataEncryptionKeyId=fixture-not-production"
  printf '%s\n' "ProductionCredentials__DataEncryptionKey=${key_b64}"
  printf '%s\n' "OTHER_SECRET=present"
} > "$dump_file"
dump_dir="/tmp/kit-credential-pull-fixture-extract"
stage_dirs+=("$dump_dir")
mapfile -t dump_run < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$dump_file" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="$dump_dir" bash "$stage")
assert_token "0" "${dump_run[0]}"
assert_token "PRODUCTION_CREDENTIAL_KEY_PULL_FIXTURE_STAGED" "$(head -n1 "$work/out.txt")"
extract_path="$(sed -n 's/^PULL_PATH=//p' "$work/out.txt" | head -n1)"
extract_lines="$(grep -cve '^[[:space:]]*$' "$extract_path")"
if [[ "$extract_lines" != "2" ]]; then
  printf '%s\n' "FIXTURE_EXTRACT_LINE_COUNT=${extract_lines}"
  exit 1
fi
if grep -F "UNRELATED=" "$extract_path" >/dev/null 2>&1 || grep -F "OTHER_SECRET=" "$extract_path" >/dev/null 2>&1; then
  printf '%s\n' "FIXTURE_EXTRACT_LEAKED"
  exit 1
fi
printf '%s\n' "FIXTURE_EXTRACT_TWO_LINES"
mapfile -t extract_discard < <(run_stage env KIT_CREDENTIAL_PULL_FIXTURE=true bash "$discard" "$extract_path")
assert_token "0" "${extract_discard[0]}"
assert_token "PRODUCTION_CREDENTIAL_KEY_PULL_STAGE_DISCARDED" "$(head -n1 "$work/out.txt")"

# Successful fixture stage, second stage refused, discard removes the file.
good_dir="/tmp/kit-credential-pull-fixture-good"
stage_dirs+=("$good_dir")
mapfile -t good_run < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$env_file" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="$good_dir" bash "$stage")
assert_token "0" "${good_run[0]}"
assert_token "PRODUCTION_CREDENTIAL_KEY_PULL_FIXTURE_STAGED" "$(head -n1 "$work/out.txt")"
pull_path="$(sed -n 's/^PULL_PATH=//p' "$work/out.txt" | head -n1)"
if [[ ! "$pull_path" =~ ^/tmp/kit-credential-pull-fixture-good/key-[0-9a-f]{32}\.env$ ]]; then
  printf '%s\n' "FIXTURE_PULL_PATH_REJECTED"
  exit 1
fi
if [[ ! -f "$pull_path" ]]; then
  printf '%s\n' "FIXTURE_STAGE_FILE_MISSING"
  exit 1
fi
mode="$(stat -c %a "$pull_path" 2>/dev/null || stat -f %OLp "$pull_path")"
case "$(uname -s)" in
  MINGW*|MSYS*)
    if [[ "$mode" != "400" && "$mode" != "444" ]]; then
      printf '%s\n' "FIXTURE_MODE=${mode}"
      exit 1
    fi
    if ! grep -q 'chmod 400 "$dest"' "$stage"; then
      printf '%s\n' "FIXTURE_CHMOD_MISSING"
      exit 1
    fi
    printf '%s\n' "FIXTURE_STAGE_MODE_POSIX_UNVERIFIED=${mode}"
    ;;
  *)
    if [[ "$mode" != "400" ]]; then
      printf '%s\n' "FIXTURE_MODE=${mode}"
      exit 1
    fi
    printf '%s\n' "FIXTURE_STAGE_MODE=400"
    ;;
esac

mapfile -t second < <(run_stage env PULL_USER="$pull_user" ENV_FILE="$env_file" KIT_CREDENTIAL_PULL_FIXTURE=true OFFMACHINE_CONFIRMED=true PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED=true PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true STAGE_DIR="$good_dir" bash "$stage")
assert_token "PRODUCTION_CREDENTIAL_KEY_PULL_STAGE_EXISTS" "$(head -n1 "$work/out.txt")"
assert_token "1" "${second[0]}"
printf '%s\n' "FIXTURE_SECOND_STAGE_REJECTED"

mapfile -t discarded < <(run_stage env KIT_CREDENTIAL_PULL_FIXTURE=true bash "$discard" "$pull_path")
assert_token "0" "${discarded[0]}"
assert_token "PRODUCTION_CREDENTIAL_KEY_PULL_STAGE_DISCARDED" "$(head -n1 "$work/out.txt")"
if [[ -e "$pull_path" || -d "$good_dir" ]]; then
  printf '%s\n' "FIXTURE_DISCARD_LEFT_FILE"
  exit 1
fi
printf '%s\n' "FIXTURE_DISCARD_OK"

# Discard must not accept the production env file or a production-looking path outside the stage directory.
mapfile -t refuse_env < <(run_stage env bash "$discard" "/etc/kit-platform/api.env")
assert_token "PRODUCTION_CREDENTIAL_KEY_BACKUP_SAME_MACHINE" "$(head -n1 "$work/out.txt")"
assert_token "1" "${refuse_env[0]}"
printf '%s\n' "FIXTURE_DISCARD_ENV_REJECTED"

mapfile -t refuse_other < <(run_stage env bash "$discard" "/tmp/key-0123456789abcdef0123456789abcdef.env")
assert_token "PRODUCTION_CREDENTIAL_KEY_BACKUP_REQUIRED" "$(head -n1 "$work/out.txt")"
assert_token "1" "${refuse_other[0]}"
printf '%s\n' "FIXTURE_DISCARD_OTHER_PATH_REJECTED"

unset key_b64
printf '%s\n' "PULL_STAGE_FIXTURE_OK"
