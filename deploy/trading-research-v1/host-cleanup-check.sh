#!/bin/bash
set -u
echo "API_PID=$(systemctl show kit-platform-api -p MainPID --value)"
echo "API_HEALTH=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 15 http://127.0.0.1:5000/api/health || echo curlfail)"
if id kit-research >/dev/null 2>&1; then echo "USER=present"; else echo "USER=absent"; fi
if [ -d /opt/kit-platform/trading-research ]; then echo "OPT=present"; else echo "OPT=absent"; fi
if [ -f /etc/systemd/system/kit-trading-research.service ]; then echo "UNIT=present"; else echo "UNIT=absent"; fi
echo "DB=$(sudo -u postgres psql -d postgres -tA -c "SELECT COALESCE(string_agg(datname,','),'none') FROM pg_database WHERE datname='trading_research';")"
echo "ROLE=$(sudo -u postgres psql -d postgres -tA -c "SELECT COALESCE(string_agg(rolname,','),'none') FROM pg_roles WHERE rolname='trading_research';")"
hba=$(sudo -u postgres psql -d postgres -tA -c 'SHOW hba_file;')
if grep -q 'kit-trading-research-deny-novixa' "$hba"; then echo "HBA_MARKER=present"; else echo "HBA_MARKER=absent"; fi
echo "COREDUMP_BEGIN"
coredumpctl list --no-pager 2>/dev/null | awk 'NR==1 || /dotnet|kit-trading/' || echo "COREDUMP_NONE"
echo "COREDUMP_END"
