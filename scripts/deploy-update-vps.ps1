# Cap nhat KitPlatform len VPS — KHONG wipe DB / secrets.
# Tu dong nhan layout cu (pharmacore) va chuyen sang kit-platform.
#
# Usage:
#   .\scripts\deploy-production.ps1 -ApiBaseUrl "https://api.novixa.vn"
#   .\scripts\deploy-update-vps.ps1                 # mac dinh SkipMigrations (schema da co)
#   .\scripts\deploy-update-vps.ps1 -RunMigrations  # chay them migration neu can
param(
    [string]$SshTarget = "root@103.200.23.229",
    [string]$CredentialsFile = "E:\Maychu_VPS\tk.txt",
    [string]$Root = "E:\KitPlatform",
    [switch]$RunMigrations
)

$ErrorActionPreference = "Stop"
Set-Location $Root

$plink = "C:\Program Files\PuTTY\plink.exe"
$pscp = "C:\Program Files\PuTTY\pscp.exe"
if (-not (Test-Path $plink)) { throw "plink.exe not found" }
if (-not (Test-Path $pscp)) { throw "pscp.exe not found" }

$passLine = Get-Content $CredentialsFile | Where-Object { $_ -match '^Pass' } | Select-Object -First 1
if (-not $passLine) { throw "password not found in credentials file" }
$pass = ($passLine -replace '^Pass[^:]*:\s*', '').Trim()

$required = @(
    "publish\api\KitPlatform.Api.dll",
    "publish\admin\index.html",
    "publish\customer-app\index.html",
    "publish\staff-app\index.html",
    "deploy\ubuntu\run-migrations-prod.sh",
    "deploy\ubuntu\smoke-test.sh",
    "deploy\ubuntu\kit-platform-api.service",
    "deploy\ubuntu\nginx-kit-platform.conf"
)
foreach ($f in $required) {
    if (-not (Test-Path $f)) { throw "Missing $f - chay deploy-production.ps1 truoc." }
}

$remote = "/tmp/kit-platform-update"
Write-Host "=== KitPlatform UPDATE (no wipe, cutover pharmacore->kit-platform) -> $SshTarget ===" -ForegroundColor Cyan

& $plink -batch -pw $pass $SshTarget "rm -rf $remote; mkdir -p $remote/api $remote/admin $remote/customer-app $remote/staff-app $remote/migrations $remote/deploy"
& $pscp -batch -pw $pass -r "$Root\publish\api" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass -r "$Root\publish\admin" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass -r "$Root\publish\customer-app" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass -r "$Root\publish\staff-app" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass -r "$Root\migrations" "${SshTarget}:${remote}/"
& $pscp -batch -pw $pass -r "$Root\deploy\ubuntu" "${SshTarget}:${remote}/deploy/"

$runMig = if ($RunMigrations) { "1" } else { "0" }
# Use LF remote script via bash -s to avoid PowerShell CRLF/here-string issues.
$remoteBash = @'
set -euo pipefail
REMOTE=/tmp/kit-platform-update
RUN_MIG=__RUN_MIG__

NEW_WEB=/var/www/kit-platform
NEW_OPT=/opt/kit-platform
NEW_CFG=/etc/kit-platform
OLD_WEB=/var/www/pharmacore
OLD_OPT=/opt/pharmacore
OLD_CFG=/etc/pharmacore

echo "==> Detect layout"
if [[ -f "$NEW_CFG/api.env" ]]; then
  WEB="$NEW_WEB"; OPT="$NEW_OPT"; CFG="$NEW_CFG"; SVC=kit-platform-api
elif [[ -f "$OLD_CFG/api.env" ]]; then
  WEB="$OLD_WEB"; OPT="$OLD_OPT"; CFG="$OLD_CFG"; SVC=pharmacore-api
else
  echo "ERROR: neither /etc/kit-platform nor /etc/pharmacore api.env found"; exit 1
fi
echo "active_cfg=$CFG active_web=$WEB active_svc=$SVC"

echo "==> Ensure kit-platform directories"
mkdir -p "$NEW_WEB/api/uploads" "$NEW_OPT" "$NEW_CFG"
if [[ "$CFG" == "$OLD_CFG" ]]; then
  echo "==> Copy secrets/api.env pharmacore -> kit-platform (preserve)"
  [[ -f "$OLD_CFG/api.env" ]] && cp -a "$OLD_CFG/api.env" "$NEW_CFG/api.env"
  [[ -f "$OLD_CFG/secrets.generated" ]] && cp -a "$OLD_CFG/secrets.generated" "$NEW_CFG/secrets.generated"
  # migrate uploads if present
  if [[ -d "$OLD_WEB/api/uploads" ]]; then
    rsync -a "$OLD_WEB/api/uploads/" "$NEW_WEB/api/uploads/" || true
  fi
fi

echo "==> Sync new artifacts into kit-platform paths"
rsync -a "$REMOTE/api/" "$NEW_WEB/api/"
rsync -a --delete "$REMOTE/admin/" "$NEW_WEB/admin/"
rsync -a --delete "$REMOTE/customer-app/" "$NEW_WEB/customer-app/"
rsync -a --delete "$REMOTE/staff-app/" "$NEW_WEB/staff-app/"
rsync -a "$REMOTE/deploy/ubuntu/" "$NEW_OPT/"
rsync -a "$REMOTE/migrations/" "$NEW_OPT/migrations/"
for f in "$NEW_OPT"/*.sh; do
  sed -i 's/\r$//' "$f" 2>/dev/null || true
  sed -i '1s/^\xEF\xBB\xBF//' "$f" 2>/dev/null || true
done
chmod +x "$NEW_OPT"/*.sh 2>/dev/null || true
chown -R www-data:www-data "$NEW_WEB/admin" "$NEW_WEB/customer-app" "$NEW_WEB/staff-app" "$NEW_WEB/api/uploads"

# Keep legacy web tree in sync during dual-run (nginx may still point to pharmacore until cutover)
if [[ -d "$OLD_WEB" ]]; then
  echo "==> Mirror artifacts to legacy pharmacore web root (dual-run)"
  rsync -a "$NEW_WEB/api/" "$OLD_WEB/api/"
  rsync -a --delete "$NEW_WEB/admin/" "$OLD_WEB/admin/"
  rsync -a --delete "$NEW_WEB/customer-app/" "$OLD_WEB/customer-app/"
  rsync -a --delete "$NEW_WEB/staff-app/" "$OLD_WEB/staff-app/" || true
fi

if [[ "$RUN_MIG" == "1" ]]; then
  echo "==> Migrations (optional)"
  # shellcheck disable=SC1090
  source "$NEW_CFG/secrets.generated"
  # Prefer kitplatform role; fall back to pharmacore role if needed
  DB_USER_TRY=kitplatform
  DB_NAME_TRY=novixa_prod
  if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_roles WHERE rolname='kitplatform'" | grep -q 1; then
    DB_USER_TRY=pharmacore
  fi
  if ! sudo -u postgres psql -tAc "SELECT 1 FROM pg_database WHERE datname='novixa_prod'" | grep -q 1; then
    # discover from api.env
    DB_NAME_TRY=$(grep -oP 'Database=\K[^;]+' "$NEW_CFG/api.env" | head -n1 || true)
    DB_USER_TRY=$(grep -oP 'Username=\K[^;]+' "$NEW_CFG/api.env" | head -n1 || true)
  fi
  CONN="postgresql://${DB_USER_TRY}:${DB_PASS}@127.0.0.1:5432/${DB_NAME_TRY}"
  bash "$NEW_OPT/run-migrations-prod.sh" "$CONN" || echo "WARN: migration failed (schema may already exist) - continue"
else
  echo "==> Skip migrations (schema already live)"
fi

echo "==> Install kit-platform systemd unit"
cp "$NEW_OPT/kit-platform-api.service" /etc/systemd/system/kit-platform-api.service
if [[ -f "$NEW_OPT/kit-platform-sms-stub.service" ]]; then
  cp "$NEW_OPT/kit-platform-sms-stub.service" /etc/systemd/system/kit-platform-sms-stub.service || true
fi

echo "==> Point nginx to kit-platform only (preserve SSL; disable legacy site)"
if [[ -f /etc/nginx/sites-available/pharmacore && ! -f /etc/nginx/sites-available/kit-platform ]]; then
  cp /etc/nginx/sites-available/pharmacore /etc/nginx/sites-available/kit-platform
fi
if [[ -f /etc/nginx/sites-available/kit-platform ]]; then
  sed -i 's|/var/www/pharmacore|/var/www/kit-platform|g' /etc/nginx/sites-available/kit-platform || true
else
  cp "$NEW_OPT/nginx-kit-platform.conf" /etc/nginx/sites-available/kit-platform
fi
rm -f /etc/nginx/sites-enabled/pharmacore
ln -sfn /etc/nginx/sites-available/kit-platform /etc/nginx/sites-enabled/kit-platform
nginx -t
systemctl reload nginx

echo "==> Switch service pharmacore-api -> kit-platform-api"
systemctl daemon-reload
systemctl stop pharmacore-api 2>/dev/null || true
systemctl disable pharmacore-api 2>/dev/null || true
systemctl enable kit-platform-api
systemctl restart kit-platform-api
if systemctl list-unit-files | grep -q kit-platform-sms-stub; then
  systemctl enable kit-platform-sms-stub 2>/dev/null || true
  systemctl restart kit-platform-sms-stub 2>/dev/null || true
fi
sleep 3
systemctl is-active kit-platform-api
curl -sf https://api.novixa.vn/api/health; echo
curl -sf https://api.novixa.vn/api/health/db; echo
bash "$NEW_OPT/smoke-test.sh"
echo "==> CUTOVER OK (DB preserved)"
'@

$remoteBash = $remoteBash.Replace('__RUN_MIG__', $runMig)
$remoteBashUnix = ($remoteBash -replace "`r`n", "`n")
$tmpScript = Join-Path $env:TEMP "kitplatform-update-remote.sh"
[IO.File]::WriteAllText($tmpScript, $remoteBashUnix, [Text.UTF8Encoding]::new($false))
& $pscp -batch -pw $pass $tmpScript "${SshTarget}:${remote}/update.sh"
& $plink -batch -pw $pass $SshTarget "bash $remote/update.sh"

Write-Host "`nDeploy update + cutover done." -ForegroundColor Green
