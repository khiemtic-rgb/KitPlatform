#!/bin/bash
set -u
echo "FOLLOWUP $(date -u +%Y-%m-%dT%H:%M:%SZ)"
echo "SECTION comms"
ps -eo comm= | sed 's/[[:space:]].*//' | sort | uniq -c | sort -nr | head -n 60
echo "SECTION www-data"
ps -u www-data -o pid,ppid,lstart,comm
echo "SECTION listeners"
ss -ltnp | awk '{print $1, $4, $6}'
echo "SECTION unit-files"
find /etc/systemd/system /lib/systemd/system -maxdepth 2 -type f \( -name '*.service' -o -name '*.timer' \) | awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|ccxt|exchange|bybit|binance|okx|freqtrade|hummingbot|second-brain/ {print}'
echo "UNIT_FILE_COUNT $(find /etc/systemd/system -maxdepth 2 -type f -name '*.service' | wc -l)"
echo "SECTION opt"
find /opt /var/lib -maxdepth 2 -type d -printf '%p\n' 2>/dev/null | awk 'BEGIN{IGNORECASE=1} $0 ~ /trad|ccxt|exchange|bybit|binance|okx|freqtrade|bot|kit-platform/ {print}'
echo "SECTION schema"
sudo -u postgres psql -d novixa_prod -Atc "SELECT COUNT(*) FROM pg_namespace WHERE nspname = 'pack_personal_intelligence';"
echo "FOLLOWUP_DONE"
