#!/bin/bash
# Install a Second Brain Agent candidate on VPS B. Do not run until deploy approval.
# This script does not print secrets, does not migrate, and does not change agent.env.
set -euo pipefail

if [[ "${APPROVE:-}" != "yes" ]]; then
  echo "Refusing to install. Re-run with APPROVE=yes after the release candidate is approved." >&2
  exit 2
fi

: "${ARTIFACT:?Set ARTIFACT to the unpacked release directory}"
: "${EXPECTED_HOST_SHA256:?Set EXPECTED_HOST_SHA256 to the published host DLL hash}"
: "${ROLLBACK_HOST_SHA256:?Set ROLLBACK_HOST_SHA256 to the host DLL hash that is running now}"
: "${RELEASE_ID:?Set RELEASE_ID to the short commit id}"

SERVICE=second-brain-agent
ENV_FILE=/etc/second-brain-agent/agent.env
UNIT_DROPIN=/etc/systemd/system/second-brain-agent.service.d/v1-${RELEASE_ID}.conf
PREVIOUS_DROPIN=/etc/systemd/system/second-brain-agent.service.d/v1-5f29e1bd.conf
TARGET=/opt/second-brain-agent-v1-${RELEASE_ID}
HOST_DLL="${ARTIFACT}/publish/host/KitPlatform.SecondBrainAgent.Host.dll"
HEALTH_URL=http://127.0.0.1:5095/api/health
DB_HEALTH_URL=http://127.0.0.1:5095/api/health/db

if [[ "${TARGET_HOST:-}" != "vps-b" ]]; then
  echo "Refusing to install. Set TARGET_HOST=vps-b only on 157.10.53.182." >&2
  exit 2
fi

test -f "$HOST_DLL"
test -f "$ENV_FILE"
test -f "$PREVIOUS_DROPIN"
actual=$(sha256sum "$HOST_DLL" | awk '{print toupper($1)}')
expected=$(printf '%s' "$EXPECTED_HOST_SHA256" | tr '[:lower:]' '[:upper:]')
if [[ "$actual" != "$expected" ]]; then
  echo "Refusing to install. Artifact host DLL hash does not match." >&2
  exit 2
fi

current_dir=$(systemctl show "$SERVICE" -p WorkingDirectory --value)
current_dll="${current_dir}/host/KitPlatform.SecondBrainAgent.Host.dll"
if [[ ! -f "$current_dll" ]]; then
  current_dll="${current_dir}/KitPlatform.SecondBrainAgent.Host.dll"
fi
running=$(sha256sum "$current_dll" | awk '{print toupper($1)}')
rollback=$(printf '%s' "$ROLLBACK_HOST_SHA256" | tr '[:lower:]' '[:upper:]')
if [[ "$running" != "$rollback" ]]; then
  echo "Refusing to install. The running host DLL is not the approved rollback binary." >&2
  exit 2
fi

if grep -Eq '5432|novixa_prod|103\.200\.23\.229' "$ENV_FILE"; then
  echo "Refusing to install. Environment points at a forbidden endpoint." >&2
  exit 2
fi
if grep -Eq '^PersonalIntelligence__ExchangeBackfill__WorkerEnabled=true' "$ENV_FILE"; then
  echo "Refusing to install. Exchange backfill worker is enabled." >&2
  exit 2
fi
if grep -Eq '^PersonalIntelligence__BusinessIntent__Enabled=true' "$ENV_FILE"; then
  echo "Refusing to install while the model is enabled. Approve model credentials separately." >&2
  exit 2
fi

install -d -o sbagent -g sbagent "$TARGET"
cp -a "${ARTIFACT}/publish/host" "$TARGET/host"
chown -R sbagent:sbagent "$TARGET"
install -d /var/backups
cp -a "$PREVIOUS_DROPIN" "/var/backups/second-brain-agent-dropin-5f29e1bd.conf"

cat > "$UNIT_DROPIN" <<EOF
[Service]
WorkingDirectory=${TARGET}/host
ExecStart=
ExecStart=/usr/bin/dotnet ${TARGET}/host/KitPlatform.SecondBrainAgent.Host.dll
EOF
rm -f "$PREVIOUS_DROPIN"
systemctl daemon-reload
systemctl restart "$SERVICE"

rollback_now() {
  echo "Health failed. Restoring the previous drop-in." >&2
  rm -f "$UNIT_DROPIN"
  cp -a "/var/backups/second-brain-agent-dropin-5f29e1bd.conf" "$PREVIOUS_DROPIN"
  systemctl daemon-reload
  systemctl restart "$SERVICE"
  exit 1
}

for _ in 1 2 3 4 5 6 7 8 9 10; do
  if curl -fsS "$HEALTH_URL" | grep -q '"status":"ok"' && curl -fsS "$DB_HEALTH_URL" | grep -q '"status":"ok"'; then
    systemctl is-active --quiet "$SERVICE"
    echo "Installed ${RELEASE_ID}. Health is ok. Model remains disabled."
    exit 0
  fi
  sleep 1
done
rollback_now
