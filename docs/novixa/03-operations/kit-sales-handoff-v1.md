# KIT Sales — handoff (park riêng)

Paste vào **New Chat** (Ctrl+L / New Agent) để tiếp epic.

## Identity
- Pack/module/package: `kit_sales` | Schema: `pack_sales` | Tenant: **`KIT_SALES`**
- Manifest: `deploy/ubuntu/migration-files.kit-sales.txt` only
- **Not** Pharmacy POS (`sales`), **not** Growth Desk (`/success/growth`), **not** `KIT_MKT`
- Login: `KIT_SALES` / `admin` / `Admin@123`
- Rule: `.cursor/rules/kit-sales-park.mdc`

## Done (local + partly on main)
- Mig 323 schema + 324 `KIT_SALES` tenant + 325 source TEXT
- Pack `src/Packs/Sales/` + `api/kit-sales` + admin `/kit-sales`
- Dedicated tenant isolation (strip `kit_sales` from `KIT_MKT`)
- Fix redirect loop: marketing dashboard → `/kit-sales` when no `kit_content`
- Fix Dapper `DateTime`/`DateTimeOffset` mapping for create/list
- Lead **edit/delete** UI + `PUT/DELETE api/kit-sales/leads/{id}`
- Phase 1 desk: lead drawer, pipeline filter + stage next/prev, interaction log, next-action task
- `GET api/kit-sales/leads/{id}` · `POST .../interactions` · `POST .../tasks` · `POST api/kit-sales/tasks/{id}/complete`

## Likely uncommitted / to verify
- Redirect + Dapper fix + edit/delete + Login `KIT_SALES` button + AppLayout org label — check `git status` before commit
- Phase 1 drawer/API — include with kit-sales; restart API after pull (`:5290`)
- Famixa WIP / `App_Data` — do **not** include in kit-sales commits

## Facebook journey (outbound, quiet hours)
- 4 bước: opener nỗi đau → mời PHC → hỏi báo cáo → Pilot 30 ngày (NVX-SALES-004)
- Nỗi đau: sau bán / cận date / vắng mặt / giao ca / chủ đứng quầy / nhân sự / báo cáo
- Lịch: **Thứ 3–5, 14:30 hoặc 20:00 giờ VN** — không đầu giờ sáng, không T2/T7 (ngày đông)
- Desk: drawer *Lộ trình chat* + hàng “Đến giờ chat”. Worker 10 phút gửi khi đã có Page token + PSID

## Facebook chat (grounded, inbound only)
- Brain: `NovixaSalesKnowledge` — chỉ copy từ founding FAQ, NVX-SALES-004, terms §7. Unknown → escalate, không bịa.
- Desk: soạn trả lời + copy `m.me/novixa68` + PHC `novixa.vn/vi/health-check/`
- Live send: `KitSales:Facebook` (Enabled, PageAccessToken, VerifyToken, AppSecret) + webhook `GET/POST /api/kit-sales/facebook/webhook`
- **Không** cold-DM fanpage nhà thuốc, không crawl Facebook, không dùng token KIT_MKT

## Next
- Gắn Page token Novixa + Meta App Review (`pages_messaging`) + HTTPS webhook
- PHC hook via Assessment (`assessment_submission_id`)
- Signals / product profile UI

## Constraints
- Pharmacy freeze: no `NT_XUANHOA` / no auto-enable on `DEMO_PHARMACY`
- Don’t merge Kit Sales + Pharmacy auth in one PR
- Admin `npm run build` blocked by Famixa TS errors on main — use Vite/tsc scoped or fix Famixa separately

## Dev
```
cd client/admin && npm run dev   # :5173 + ensure API :5290
```
