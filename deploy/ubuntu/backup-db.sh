#!/usr/bin/env bash
# Daily PostgreSQL backup for novixa_prod.
# Cron example: 0 2 * * * /opt/kit-platform/backup-db.sh
#
# The local socket uses peer authentication. pg_dump must run as OS user
# postgres and must not request another database role. A mismatched role fails, and writing the
# gzip redirect straight to the final path leaves a 20-byte archive behind.
# Publish the archive only after gzip and dump checks pass.
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/var/backups/KitPlatform}"
DB_NAME="${DB_NAME:-novixa_prod}"
RETAIN_DAYS="${RETAIN_DAYS:-14}"

mkdir -p "$BACKUP_DIR"
STAMP="$(date +%Y%m%d_%H%M%S)"
FILE="$BACKUP_DIR/${DB_NAME}_${STAMP}.sql.gz"
PARTIAL="${FILE}.partial"

fail() {
  rm -f "$PARTIAL"
  echo "BACKUP_FAILED $*" >&2
  exit 1
}

if [[ -e "$FILE" ]]; then
  echo "BACKUP_FAILED archive_exists path=$FILE" >&2
  exit 1
fi

rm -f "$PARTIAL"

actual="$(sudo -u postgres psql -d "$DB_NAME" -v ON_ERROR_STOP=1 -tAc "SELECT current_database()")"
actual="${actual//[[:space:]]/}"
if [[ "$actual" != "$DB_NAME" ]]; then
  echo "BACKUP_FAILED database_mismatch" >&2
  exit 1
fi

set +e
sudo -u postgres pg_dump -d "$DB_NAME" --no-owner --no-acl | gzip > "$PARTIAL"
pipe_status=("${PIPESTATUS[@]}")
set -e
dump_status=${pipe_status[0]}
gzip_status=${pipe_status[1]}
if [[ "$dump_status" -ne 0 || "$gzip_status" -ne 0 ]]; then
  fail "pg_dump=$dump_status gzip=$gzip_status db=$DB_NAME"
fi

if ! gzip -t "$PARTIAL"; then
  fail "gzip_test"
fi

uncompressed="$(gzip -dc "$PARTIAL" | wc -c | tr -d '[:space:]')"
if [[ ! "$uncompressed" =~ ^[0-9]+$ || "$uncompressed" -le 0 ]]; then
  fail "uncompressed_empty"
fi

if ! python3 - "$PARTIAL" << 'PY'
import gzip
import sys

path = sys.argv[1]
header = False
tenants = False
early_fatal = False
with gzip.open(path, "rt", encoding="utf-8", errors="replace") as handle:
    for number, line in enumerate(handle, 1):
        if number <= 15 and "PostgreSQL database dump" in line:
            header = True
        if number <= 8 and ("FATAL:" in line or "Peer authentication failed" in line):
            early_fatal = True
        if "CREATE TABLE public.tenants" in line:
            tenants = True
            break
if early_fatal or not header or not tenants:
    sys.exit(2)
PY
then
  fail "dump_check uncompressed=$uncompressed"
fi

mv "$PARTIAL" "$FILE"
# RETAIN_DAYS stays 14. The delete is not run until an operator sets
# RETENTION_DELETE_CONFIRMED=true after off-machine storage is confirmed.
# A same-machine hold directory is not that confirmation.
if [[ "${RETENTION_DELETE_CONFIRMED:-}" == "true" ]]; then
  find "$BACKUP_DIR" -name "${DB_NAME}_*.sql.gz" -mtime +"$RETAIN_DAYS" -delete
else
  echo "RETENTION_DELETE_SUSPENDED retain_days=$RETAIN_DAYS"
  find "$BACKUP_DIR" -name "${DB_NAME}_*.sql.gz" -mtime +"$RETAIN_DAYS" -printf 'RETENTION_WOULD_DELETE %f\n'
fi
compressed="$(stat -c %s "$FILE")"
echo "BACKUP_OK db=$DB_NAME path=$FILE bytes=$compressed uncompressed=$uncompressed"

# The dump does not contain ProductionCredentials__DataEncryptionKey.
# Credential ciphertext cannot be decrypted from this file alone.
# This call is skipped when the key-backup script is not installed.
KEY_BACKUP="$(cd "$(dirname "$0")" && pwd)/backup-production-credential-key.sh"
if [[ -f "$KEY_BACKUP" ]]; then
  bash "$KEY_BACKUP"
fi
