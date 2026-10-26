#!/bin/bash
set -u
echo "ACL_BEGIN"
sudo -u postgres psql -d postgres -tA -c "SELECT datname || '|' || COALESCE(datacl::text,'default-public') FROM pg_database WHERE datname IN ('novixa_prod','postgres','template1') ORDER BY 1;"
echo "ACL_END"
echo "HBA_BEGIN"
sudo -u postgres psql -d postgres -tA -F '|' -c "SELECT type, database::text, user_name::text, COALESCE(address,''), auth_method FROM pg_hba_file_rules ORDER BY line_number;"
echo "HBA_END"
echo "ENC=$(sudo -u postgres psql -d postgres -tA -c 'SHOW password_encryption;')"
for path in /health /healthz /ready /alive /api/health; do
  code=$(curl -sS -o /dev/null -w '%{http_code}' --max-time 10 "http://127.0.0.1:5000$path" || echo curlfail)
  echo "PROBE $path $code"
done
echo "API_STILL=$(systemctl show kit-platform-api -p MainPID -p ActiveState --no-pager | tr '\n' ' ')"
