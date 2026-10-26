#!/bin/bash
# Read-only trading automation audit. Prints names and counts, never secret values.
set -u
echo "AUDIT_TIME $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "HOST $(hostname -I | awk '{print $1}')"
echo "SECTION processes"
ps -eo pid,ppid,user,lstart,cmd --no-headers | awk '
BEGIN { IGNORECASE=1 }
$0 ~ /trad|ccxt|freqtrade|hummingbot|bybit|binance|okx|exchange|second-brain|pm2|supervisord|gunicorn/ ||
$0 ~ /(python|node|dotnet|node )/ {
  line=$0
  gsub(/[Pp]assword=[^[:space:]]+/, "password=REDACTED", line)
  gsub(/[Tt]oken=[^[:space:]]+/, "token=REDACTED", line)
  gsub(/[Ss]ecret=[^[:space:]]+/, "secret=REDACTED", line)
  gsub(/[Aa]pi[_-]?[Kk]ey=[^[:space:]]+/, "api_key=REDACTED", line)
  gsub(/Bearer [^[:space:]]+/, "Bearer REDACTED", line)
  print line
}'
echo "SECTION api-process"
pid=$(systemctl show -p MainPID --value kit-platform-api 2>/dev/null || true)
echo "API_PID ${pid:-absent}"
if [ -n "${pid:-}" ] && [ -d "/proc/$pid" ]; then
  echo "API_PPID $(awk '/^PPid:/ {print $2}' /proc/$pid/status)"
  echo "API_CWD $(readlink -f /proc/$pid/cwd 2>/dev/null || echo unreadable)"
  echo "API_LSTART $(ps -o lstart= -p "$pid" 2>/dev/null || true)"
  echo "API_CMDLINE_PRESENT yes"
  tr '\0' '\n' <"/proc/$pid/environ" | awk -F= 'BEGIN{IGNORECASE=1} $1 ~ /exchange|trading|bybit|binance|okx|ccxt|credential|suppressbackground/ {print "API_ENV_NAME " $1 " " (length($2)?"set":"empty")}'
fi
echo "SECTION listeners"
ss -ltnp 2>/dev/null | awk 'BEGIN{IGNORECASE=1} $0 ~ /python|node|freqtrade|hummingbot|ccxt|trad/ {print}'
echo "SECTION systemd-units"
systemctl list-units --type=service --all --no-legend --no-pager | awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|bot|ccxt|exchange|bybit|binance|okx|freqtrade|hummingbot|second-brain|pm2|supervisor/ {print}'
echo "SECTION systemd-enabled-match"
systemctl list-unit-files --type=service --state=enabled --no-legend --no-pager | awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|bot|ccxt|exchange|bybit|binance|okx|freqtrade|hummingbot|second-brain|pm2|supervisor/ {print}'
echo "SECTION timers"
systemctl list-timers --all --no-pager
echo "SECTION kit-api-unit"
systemctl is-active kit-platform-api || true
systemctl is-enabled kit-platform-api || true
systemctl show -p Id -p FragmentPath -p DropInPaths -p User -p WorkingDirectory -p MainPID kit-platform-api
echo "SECTION cron"
for u in root www-data postgres; do
  if crontab -u "$u" -l >/tmp/sb-cron-$u 2>/dev/null; then
    echo "CRON_USER $u present"
    awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|ccxt|exchange|bybit|binance|okx|freqtrade|bot/ {gsub(/[Pp]assword=[^[:space:]]+/, "password=REDACTED"); print}' /tmp/sb-cron-$u
  else
    echo "CRON_USER $u none"
  fi
  rm -f /tmp/sb-cron-$u
done
echo "CRON_D_FILES"
find /etc/cron.d /etc/cron.daily /etc/cron.hourly /var/spool/cron -type f 2>/dev/null | awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|ccxt|exchange|bybit|binance|okx|bot/ {print}'
echo "SECTION supervisors"
command -v docker >/dev/null && docker ps --format '{{.Names}} {{.Image}} {{.Status}}' || echo "DOCKER absent"
command -v podman >/dev/null && podman ps --format '{{.Names}} {{.Image}} {{.Status}}' || echo "PODMAN absent"
command -v supervisorctl >/dev/null && supervisorctl status || echo "SUPERVISOR absent"
command -v pm2 >/dev/null && pm2 jlist >/dev/null 2>&1 && echo "PM2 present" || echo "PM2 absent"
screen -ls 2>/dev/null | awk 'NR<=20 {print}' || echo "SCREEN none"
tmux ls 2>/dev/null || echo "TMUX none"
echo "SECTION files"
find /opt /srv /home /root /usr/local /var/www /etc/systemd/system -maxdepth 4 \( -iname '*trad*' -o -iname '*ccxt*' -o -iname '*freqtrade*' -o -iname '*hummingbot*' -o -iname '*bybit*' -o -iname '*second-brain*' \) ! -path '*/uploads/*' ! -path '*/node_modules/*' 2>/dev/null | head -n 80
echo "SECTION deployed-strings"
for f in /var/www/kit-platform/api/KitPlatform.Api.dll /var/www/kit-platform/api/KitPlatform.Packs.PersonalIntelligence.Infrastructure.dll /var/www/kit-platform/api/KitPlatform.Packs.PersonalIntelligence.Api.dll; do
  if [ -f "$f" ]; then
    for s in ExchangeBackfillWorker AddPersonalIntelligencePack AddPersonalIntelligenceResearch; do
      if grep -a -q -F "$s" "$f"; then echo "STRING $(basename "$f") $s present"; else echo "STRING $(basename "$f") $s absent"; fi
    done
  else
    echo "STRING_FILE_ABSENT $(basename "$f")"
  fi
done
echo "SECTION env-key-names"
awk -F= 'BEGIN{IGNORECASE=1} $1 ~ /exchange|trading|bybit|binance|okx|ccxt|credential|suppressbackground/ {print "ENV_KEY " $1 " " (length($2)?"set":"empty")}' /etc/kit-platform/api.env
echo "SECTION database"
sudo -u postgres psql -d novixa_prod -v ON_ERROR_STOP=1 -Atc "SELECT current_database();" || echo "DB_UNAVAILABLE"
sudo -u postgres psql -d novixa_prod -Atc "SELECT nspname FROM pg_namespace WHERE nspname LIKE '%personal%' OR nspname LIKE '%trad%' ORDER BY 1;" || true
sudo -u postgres psql -d novixa_prod -At <<'SQL' || true
SELECT CASE WHEN to_regclass('pack_personal_intelligence.trading_order') IS NULL THEN 'trading_order absent' ELSE 'trading_order present' END;
SELECT CASE WHEN to_regclass('pack_personal_intelligence.trading_execution') IS NULL THEN 'trading_execution absent' ELSE 'trading_execution present' END;
SELECT CASE WHEN to_regclass('pack_personal_intelligence.exchange_connection') IS NULL THEN 'exchange_connection absent' ELSE 'exchange_connection present' END;
SELECT CASE WHEN to_regclass('pack_personal_intelligence.trading_account') IS NULL THEN 'trading_account absent' ELSE 'trading_account present' END;
SQL
if sudo -u postgres psql -d novixa_prod -Atc "SELECT to_regclass('pack_personal_intelligence.trading_order') IS NOT NULL;" | grep -qx t; then
  sudo -u postgres psql -d novixa_prod -c "SELECT COALESCE(source,'(null)') AS source, COALESCE(status,'(null)') AS status, COUNT(*) AS n, MAX(submitted_at) AS latest FROM pack_personal_intelligence.trading_order GROUP BY 1,2 ORDER BY 1,2;"
fi
if sudo -u postgres psql -d novixa_prod -Atc "SELECT to_regclass('pack_personal_intelligence.trading_execution') IS NOT NULL;" | grep -qx t; then
  sudo -u postgres psql -d novixa_prod -c "SELECT COALESCE(mode,'(null)') AS mode, COUNT(*) AS n, MAX(executed_at) AS latest FROM pack_personal_intelligence.trading_execution GROUP BY 1 ORDER BY 1;"
fi
if sudo -u postgres psql -d novixa_prod -Atc "SELECT to_regclass('pack_personal_intelligence.exchange_connection') IS NOT NULL;" | grep -qx t; then
  sudo -u postgres psql -d novixa_prod -c "SELECT exchange_code, environment, status, COUNT(*) AS n, MAX(last_checked_at) AS latest FROM pack_personal_intelligence.exchange_connection GROUP BY 1,2,3 ORDER BY 1,2,3;"
fi
if sudo -u postgres psql -d novixa_prod -Atc "SELECT to_regclass('pack_personal_intelligence.trading_account') IS NOT NULL;" | grep -qx t; then
  sudo -u postgres psql -d novixa_prod -c "SELECT COUNT(*) AS trading_accounts FROM pack_personal_intelligence.trading_account;"
fi
echo "SECTION logs"
journalctl -u kit-platform-api --since '2026-07-01' --no-pager -o short-iso 2>/dev/null | awk 'BEGIN{IGNORECASE=1} $0 ~ /ExchangeBackfill|place order|order submitted|order placed|create order|submit order|bybit|binance|okx|ccxt|freqtrade/ {c++} END{print "API_JOURNAL_MATCHES " c+0}'
journalctl -u kit-platform-api --since '2026-07-01' --no-pager -o short-iso 2>/dev/null | awk 'BEGIN{IGNORECASE=1} $0 ~ /ExchangeBackfill|place order|order submitted|order placed|create order|submit order|bybit|binance|okx|ccxt|freqtrade/ {gsub(/[Pp]assword=[^[:space:]]+/, "password=REDACTED"); gsub(/[Ss]ecret=[^[:space:]]+/, "secret=REDACTED"); gsub(/Bearer [^[:space:]]+/, "Bearer REDACTED"); if (n<20) print; n++}'
echo "SECTION other-logs"
find /var/log /opt /home /root -maxdepth 3 -type f \( -iname '*trad*' -o -iname '*ccxt*' -o -iname '*freqtrade*' -o -iname '*bybit*' \) ! -path '*/uploads/*' 2>/dev/null | head -n 40
echo "AUDIT_DONE"
