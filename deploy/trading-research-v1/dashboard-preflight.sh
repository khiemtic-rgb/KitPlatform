#!/bin/bash
set -u
echo "DASH_PREFLIGHT_START"
date -u +%Y-%m-%dT%H:%M:%SZ
echo "HOST=$(hostname)"
echo "API_PID=$(systemctl show kit-platform-api -p MainPID --value)"
echo "API_ACTIVE=$(systemctl is-active kit-platform-api 2>/dev/null || true)"
echo "API_SINCE=$(systemctl show kit-platform-api -p ActiveEnterTimestamp --value)"
code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1:5000/api/health || echo curlfail)
echo "API_HEALTH=$code"
echo "RESEARCH_SERVICE=$(systemctl is-active kit-trading-research.service 2>/dev/null || true)"
echo "RESEARCH_SERVICE_SINCE=$(systemctl show kit-trading-research.service -p ActiveEnterTimestamp --value)"
echo "RESEARCH_TIMER_ENABLED=$(systemctl is-enabled kit-trading-research.timer 2>/dev/null || true)"
echo "RESEARCH_TIMER_ACTIVE=$(systemctl is-active kit-trading-research.timer 2>/dev/null || true)"
echo "RESEARCH_TIMER_SINCE=$(systemctl show kit-trading-research.timer -p ActiveEnterTimestamp --value)"
systemctl list-timers kit-trading-research.timer --no-pager | sed 's/^/TIMER /'
if [ -f /etc/systemd/system/kit-trading-research.service ]; then
  sha256sum /etc/systemd/system/kit-trading-research.service | awk '{print "RESEARCH_UNIT_SHA="$1}'
else
  echo "RESEARCH_UNIT_SHA=missing"
fi
if [ -f /etc/systemd/system/kit-trading-research.timer ]; then
  sha256sum /etc/systemd/system/kit-trading-research.timer | awk '{print "RESEARCH_TIMER_SHA="$1}'
else
  echo "RESEARCH_TIMER_SHA=missing"
fi
if [ -f /etc/systemd/system/kit-trading-research-dashboard.service ]; then echo "DASH_UNIT=present"; else echo "DASH_UNIT=absent"; fi
echo "DASH_UNIT_STATE=$(systemctl is-active kit-trading-research-dashboard.service 2>/dev/null || true)"
echo "LISTEN_5088_BEGIN"
ss -ltn | awk '/:5088/ {print}' || true
echo "LISTEN_5088_END"
echo "LISTEN_5432_BEGIN"
ss -ltn | awk '/:5432/ {print}' || true
echo "LISTEN_5432_END"
if [ -d /opt/kit-platform/trading-research-dashboard ]; then echo "DASH_OPT=present"; else echo "DASH_OPT=absent"; fi
if [ -d /var/lib/kit-platform/trading-research-dashboard ]; then echo "DASH_VAR=present"; else echo "DASH_VAR=absent"; fi
if [ -f /etc/kit-platform/trading-research-dashboard.env ]; then echo "DASH_ENV=present"; else echo "DASH_ENV=absent"; fi
if id kit-research-dashboard >/dev/null 2>&1; then echo "DASH_USER=present"; else echo "DASH_USER=absent"; fi
echo "DOTNET_BEGIN"
/usr/bin/dotnet --list-runtimes 2>/dev/null || echo "DOTNET_MISSING"
echo "DOTNET_END"
echo "DBS_BEGIN"
sudo -u postgres psql -d postgres -tA -c "SELECT datname FROM pg_database WHERE datname IN ('trading_research','novixa_prod') ORDER BY 1;"
echo "DBS_END"
echo "ROLES_BEGIN"
sudo -u postgres psql -d postgres -tA -c "SELECT rolname || '|' || rolsuper || '|' || rolcreatedb || '|' || rolcreaterole FROM pg_roles WHERE rolname IN ('trading_research','trading_research_read') ORDER BY 1;"
echo "ROLES_END"
echo "WRITER_PRIVS_BEGIN"
sudo -u postgres psql -d trading_research -tA -c "SELECT privilege_type FROM information_schema.role_table_grants WHERE grantee='trading_research' AND table_schema='research_trading' GROUP BY privilege_type ORDER BY 1;"
echo "WRITER_PRIVS_END"
echo "IDENTITY_BEGIN"
sudo -u postgres psql -d trading_research -tA -c "SELECT current_database() || '|' || current_setting('port');"
echo "IDENTITY_END"
hba=$(sudo -u postgres psql -d postgres -tA -c 'SHOW hba_file;')
echo "HBA_FILE_SET=$([ -n "$hba" ] && echo yes || echo no)"
echo "HBA_RESEARCH_DENY=$(grep -c 'kit-trading-research-deny-novixa' "$hba" || true)"
echo "HBA_DASH_DENY=$(grep -c 'kit-trading-research-dashboard-deny-novixa' "$hba" || true)"
echo "DASH_PREFLIGHT_DONE"
