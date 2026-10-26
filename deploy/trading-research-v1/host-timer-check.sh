#!/bin/bash
set -u
echo "WAIT_START $(date -u +%Y-%m-%dT%H:%M:%SZ)"
target=$(date -u -d '2026-10-23 07:01:10' +%s)
now=$(date -u +%s)
if [ "$now" -lt "$target" ]; then
  sleep $((target - now))
fi
echo "WAIT_END $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "API_PID=$(systemctl show kit-platform-api -p MainPID --value)"
echo "API_ACTIVE=$(systemctl show kit-platform-api -p ActiveState --value)"
echo "API_HEALTH=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1:5000/api/health || echo curlfail)"
echo "ADMIN_HTTP=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 https://admin.novixa.vn/ || echo curlfail)"
echo "APP_HTTP=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 20 https://app.novixa.vn/ || echo curlfail)"
echo "TIMER_ENABLED=$(systemctl is-enabled kit-trading-research.timer 2>/dev/null || true)"
echo "TIMER_ACTIVE=$(systemctl is-active kit-trading-research.timer 2>/dev/null || true)"
systemctl show kit-trading-research.service -p ExecMainStatus -p Result -p ExecMainStartTimestamp --no-pager | sed 's/^/SERVICE_/'
systemctl list-timers kit-trading-research.timer --no-pager
if journalctl -u kit-trading-research.service --since '2026-10-23 13:59:00' --no-pager | grep -qE 'Password=|/api/v3/order|testnet.binance'; then
  echo "TIMER_SECRET_OR_ORDER=yes"
else
  echo "TIMER_SECRET_OR_ORDER=no"
  journalctl -u kit-trading-research.service --since '2026-10-23 13:59:00' --no-pager -o cat
fi
echo "SQL_BEGIN"
sudo -u postgres psql -d trading_research -c "SELECT COUNT(*) AS candles FROM research_trading.candle;"
sudo -u postgres psql -d trading_research -c "SELECT COUNT(*) AS fills FROM research_trading.paper_fill;"
sudo -u postgres psql -d trading_research -c "SELECT COUNT(*) AS operations FROM research_trading.paper_operation;"
sudo -u postgres psql -d trading_research -c "SELECT status, COUNT(*) FROM research_trading.health GROUP BY status ORDER BY status;"
sudo -u postgres psql -d trading_research -c "SELECT side, quantity, execution_price, fee, slippage_quote FROM research_trading.paper_fill ORDER BY side;"
echo "SQL_END"
echo "BACKUPS_BEGIN"
sha256sum /var/backups/kit-trading-research/*.dump
echo "BACKUPS_END"
echo "LISTEN_5000=$(ss -ltn | awk '/:5000/ {print}')"
echo "RESEARCH_PROCS=$(ps -u kit-research -o pid= | wc -l | tr -d ' ')"
echo "TIMER_CHECK_DONE"
