#!/bin/bash
set -u
echo "PREFLIGHT_START"
date -u +%Y-%m-%dT%H:%M:%SZ
echo "HOST=$(hostname)"
systemctl show kit-platform-api -p ActiveState -p SubState -p MainPID -p ActiveEnterTimestamp --no-pager | sed 's/^/API_/'
echo "API_ENABLED=$(systemctl is-enabled kit-platform-api 2>/dev/null || true)"
echo "NGINX=$(systemctl is-active nginx 2>/dev/null || true)"
echo "DOTNET_BEGIN"
/usr/bin/dotnet --list-runtimes 2>/dev/null || echo "DOTNET_MISSING"
echo "DOTNET_END"
df -B1 --output=target,avail / /opt /var /var/backups 2>/dev/null | sed 's/^/DF /'
if id kit-research >/dev/null 2>&1; then echo "USER=present"; else echo "USER=absent"; fi
if [ -d /opt/kit-platform/trading-research ]; then echo "OPT=present"; else echo "OPT=absent"; fi
if [ -f /etc/kit-platform/trading-research.env ]; then echo "ENV=present"; else echo "ENV=absent"; fi
if [ -f /etc/systemd/system/kit-trading-research.service ]; then echo "UNIT=present"; else echo "UNIT=absent"; fi
if [ -f /etc/systemd/system/kit-trading-research.timer ]; then echo "TIMER=present"; else echo "TIMER=absent"; fi
echo "API_ENV_MODE=$(stat -c '%a %U %G' /etc/kit-platform/api.env 2>/dev/null || echo missing)"
echo "DBS_BEGIN"
sudo -u postgres psql -d postgres -tA -c "SELECT datname FROM pg_database WHERE datname IN ('trading_research','novixa_prod') ORDER BY 1;"
echo "DBS_END"
echo "ACL_BEGIN"
sudo -u postgres psql -d postgres -tA -c "SELECT COALESCE(datacl,'(null)') FROM pg_database WHERE datname='novixa_prod';"
echo "ACL_END"
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1:5000/health || echo curlfail)
echo "API_HEALTH=$code"
admin=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 https://admin.novixa.vn/ || echo curlfail)
app=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 https://app.novixa.vn/ || echo curlfail)
echo "ADMIN_HTTP=$admin"
echo "APP_HTTP=$app"
tmp=$(mktemp)
hdr=$(mktemp)
kcode=$(curl -sS -D "$hdr" -o "$tmp" -w '%{http_code}' --max-time 20 'https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval=1h&limit=2' || echo curlfail)
echo "KLINE_HTTP=$kcode"
echo "KLINE_BYTES=$(wc -c < "$tmp" | tr -d ' ')"
echo "KLINE_WEIGHT=$(awk 'BEGIN{IGNORECASE=1} /^x-mbx-used-weight-1m:/ {gsub(/\r/,""); print $2}' "$hdr")"
echo "KLINE_ALLOW=$(awk 'BEGIN{IGNORECASE=1} /^access-control-allow-methods:/ {gsub(/\r/,""); print}' "$hdr")"
KLINE_FILE="$tmp" python3 -c 'import json,os; rows=json.load(open(os.environ["KLINE_FILE"],encoding="utf-8")); print("KLINE_ROWS=%s"%len(rows)); print("KLINE_OPEN=%s"%rows[0][0]); print("KLINE_CLOSE=%s"%rows[-1][6])'
rm -f "$tmp" "$hdr"
echo "LISTEN_5000=$(ss -ltn | awk '/:5000/ {print}')"
echo "PREFLIGHT_DONE"
