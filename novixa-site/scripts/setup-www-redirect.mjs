/**
 * Create Cloudflare Redirect Rule: www.novixa.vn → https://novixa.vn (301)
 *
 * Requires env:
 *   CLOUDFLARE_API_TOKEN  — Zone.Redirect Rules Edit (+ Zone.Zone Read to resolve zone id)
 *   CF_ZONE_ID            — optional
 *
 * Usage:
 *   node scripts/setup-www-redirect.mjs
 */
const ZONE_NAME = 'novixa.vn';

async function cf(path, { method = 'GET', token, body } = {}) {
  const res = await fetch(`https://api.cloudflare.com/client/v4${path}`, {
    method,
    headers: {
      Authorization: `Bearer ${token}`,
      'Content-Type': 'application/json',
    },
    body: body ? JSON.stringify(body) : undefined,
  });
  const json = await res.json();
  if (!res.ok || !json.success) {
    const err = json.errors?.map((e) => e.message).join('; ') || res.statusText;
    throw new Error(`${method} ${path}: ${err}`);
  }
  return json.result;
}

async function resolveZoneId(token) {
  const fromEnv = process.env.CF_ZONE_ID?.trim();
  if (fromEnv) return fromEnv;
  const zones = await cf(`/zones?name=${encodeURIComponent(ZONE_NAME)}`, { token });
  const zone = Array.isArray(zones) ? zones[0] : null;
  if (!zone?.id) throw new Error(`Không tìm thấy zone ${ZONE_NAME}`);
  return zone.id;
}

async function main() {
  const token = process.env.CLOUDFLARE_API_TOKEN?.trim();
  if (!token) {
    console.error('Thiếu CLOUDFLARE_API_TOKEN (cần quyền Redirect Rules Edit).');
    process.exit(1);
  }

  const zoneId = await resolveZoneId(token);
  console.log(`Zone ${ZONE_NAME}: ${zoneId}`);

  const existing = await cf(`/zones/${zoneId}/rulesets/phases/http_request_dynamic_redirect/entrypoint`, {
    token,
  }).catch(() => null);

  const rule = {
    action: 'redirect',
    expression: '(http.host eq "www.novixa.vn")',
    description: 'www to apex',
    enabled: true,
    action_parameters: {
      from_value: {
        status_code: 301,
        target_url: {
          expression: 'concat("https://novixa.vn", http.request.uri.path)',
        },
        preserve_query_string: true,
      },
    },
  };

  if (existing?.id) {
    const rules = [...(existing.rules ?? [])];
    const idx = rules.findIndex(
      (r) => r.description === 'www to apex' || r.expression?.includes('www.novixa.vn'),
    );
    if (idx >= 0) {
      rules[idx] = { ...rules[idx], ...rule, id: rules[idx].id };
      console.log('Cập nhật rule www → apex hiện có…');
    } else {
      rules.unshift(rule);
      console.log('Thêm rule www → apex vào ruleset hiện có…');
    }
    await cf(`/zones/${zoneId}/rulesets/${existing.id}`, {
      method: 'PUT',
      token,
      body: { rules },
    });
  } else {
    console.log('Tạo ruleset redirect mới…');
    await cf(`/zones/${zoneId}/rulesets`, {
      method: 'POST',
      token,
      body: {
        name: 'novixa www redirect',
        kind: 'zone',
        phase: 'http_request_dynamic_redirect',
        rules: [rule],
      },
    });
  }

  console.log('OK — www.novixa.vn sẽ 301 về https://novixa.vn');
  console.log('Kiểm tra: curl -sI https://www.novixa.vn/vi/');
}

main().catch((err) => {
  console.error(err.message || err);
  process.exit(1);
});
