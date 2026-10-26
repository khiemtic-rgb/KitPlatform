#!/bin/bash
set -euo pipefail
date -u +%Y-%m-%dT%H:%M:%SZ
systemctl is-active kit-platform-api
systemctl is-active nginx
sha256sum \
  /var/www/kit-platform/api/KitPlatform.Api.dll \
  /var/www/kit-platform/api/KitPlatform.Packs.PersonalIntelligence.Api.dll \
  /var/backups/kit-research-20261023T052648Z/api/KitPlatform.Api.dll \
  /var/backups/kit-research-20261023T052648Z/novixa_prod.dump
stat -c '%n %s %a' /etc/kit-platform/api.env /var/backups/kit-research-20261023T052648Z/api.env
stat -c '%n %s' \
  /var/www/kit-platform/api/appsettings.json \
  /var/www/kit-platform/api/appsettings.Production.json \
  /var/www/kit-platform/api/appsettings.Development.json
du -sb /var/www/kit-platform/api/uploads
curl -sS -o /dev/null -w 'HEALTH:%{http_code}\n' http://127.0.0.1:5000/api/health
curl -sS -o /dev/null -w 'PUB:%{http_code}\n' http://127.0.0.1:5000/api/personal-intelligence/research/publication
curl -sS -o /dev/null -w 'TRADE:%{http_code}\n' http://127.0.0.1:5000/api/personal-intelligence/trading/summary
curl -sk -o /dev/null -w 'ADMIN:%{http_code}\n' https://admin.novixa.vn/ || true
curl -sk -o /dev/null -w 'APP:%{http_code}\n' https://app.novixa.vn/ || true
find /var/www/kit-platform/api -name '*.sbnew' | wc -l
awk -F= '$1=="ResearchSafeHost__SuppressBackgroundWriters" {c++} END{print "SUPPRESS_LINES " c+0}' /etc/kit-platform/api.env
awk -F= '$1=="PersonalIntelligence__ResearchPublication__Root" {print "PUB_KEY_SET " (substr($0, index($0,"=")+1)=="/var/lib/kit-platform/research-publication")}' /etc/kit-platform/api.env
rm -rf /tmp/sb-research-incoming /tmp/host-backup-once.sh /tmp/host-dump-peer.sh /tmp/host-env-migrate.sh /tmp/host-install-assemblies.sh /tmp/host-restart-check.sh
printf '%s\n' "VERIFY_OK"
