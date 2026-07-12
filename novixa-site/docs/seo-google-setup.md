# Cloudflare Redirect: www → apex (novixa.vn)
#
# Cách 1 — Dashboard (khuyến nghị, ~2 phút)
# 1. https://dash.cloudflare.com → chọn zone novixa.vn
# 2. Rules → Redirect Rules → Create rule
# 3. Rule name: www to apex
# 4. If: Hostname equals www.novixa.vn
# 5. Then: Dynamic redirect
#      Expression: concat("https://novixa.vn", http.request.uri.path)
#      Status code: 301
#      Preserve query string: ON
# 6. Deploy
#
# Cách 2 — API (cần token quyền Zone.Redirect Rules Edit)
#   $env:CLOUDFLARE_API_TOKEN = "..."
#   $env:CF_ZONE_ID = "..."   # optional; script tự tìm theo novixa.vn
#   node scripts/setup-www-redirect.mjs

# Google Search Console (bắt buộc đăng nhập Google của bạn)
# 1. https://search.google.com/search-console → Add property → Domain: novixa.vn
# 2. Copy TXT record → Cloudflare DNS → Add TXT @
# 3. Verify
# 4. Sitemaps → Add: https://novixa.vn/sitemap-index.xml
# 5. URL Inspection → https://novixa.vn/vi/ → Request indexing
# 6. Lặp lại cho 1–2 bài tin tức
