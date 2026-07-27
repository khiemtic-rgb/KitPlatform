# Runbook — Family OS deploy + đồng bộ LOCAL / GIT / VPS

**Mã:** NVX-OPS-FAMILY-SYNC-01 · **Ngày:** 2026-07-24  
**Liên quan:** [pharmacy-rbac-deploy-sync-runbook-v1.md](./pharmacy-rbac-deploy-sync-runbook-v1.md) · [pharmacy-pos-customer-incidents-v1.md](./pharmacy-pos-customer-incidents-v1.md) · `.cursor/rules/family-os-deploy-sync.mdc`

### §0. Trạng thái epic — PARKED (2026-07-27)

| | |
|---|---|
| **Status** | **PARK** — tạm dừng epic / pilot deploy VPS |
| **Lý do** | Chờ phản hồi thẩm định Pharmacy (`DEMO_PHARMACY`); tránh đụng auth/layout chung và regression RBAC |
| **Ngoại lệ local** | Behavior OS Wave 0–1 (`behavior-os-north-star-v1.md`, mig `240`) được phép trên máy local khi Product yêu cầu |
| **Rule Cursor** | `.cursor/rules/family-os-parked.mdc` |
| **Unpark** | User yêu cầu rõ “tiếp Family OS / unpark” → đổi status §0 + gỡ/ cập nhật rule parked |

Pilot đã ship trước đó vẫn giữ trên VPS (`family.kittech.vn`); **không** coi PARK là gỡ prod — chỉ dừng epic mới.

---

Tài liệu ghi lại epic Family OS (chiều–tối 2026-07-24): tính năng, migration, deploy pilot, bug đã sửa, và checklist tránh **mất đồng bộ** giữa máy local, `origin/main`, và VPS.

**Nguồn chuẩn (SoT):** `origin/main` — **không** phải VPS, **không** phải working tree chưa commit.

---

## 1. Dòng thời gian (2026-07-24)

| Giai đoạn | Commit | Phạm vi |
|-----------|--------|---------|
| Sáng — Pharmacy/POS incidents (đã ghi riêng) | `229fa29` … `ad9231d` | Similar clusters, allow_credit, merge perms, POS search, Learning mail, quick-add, TS6133 — xem [pharmacy-pos-customer-incidents-v1.md](./pharmacy-pos-customer-incidents-v1.md) |
| Docs sáng | `a5cd909` | Ghi POS/customer incidents + deploy gaps |
| Chiều–tối — Family OS epic (một vertical slice) | `e55247f` | Pack 192–220, `family-app` SPA, Admin Family OS modules, pilot deploy scripts |
| Deploy fix CRLF manifest | `b76f547` | `run-family-os-migrations-prod.sh` strip `\r` khi đọc manifest Windows |

**Tip hiện tại trên `origin/main` (sau docs):** xem `git rev-parse origin/main` — deploy app Family OS đã ship tại `e55247f` + `b76f547`.

---

## 2. Tính năng Family OS (migration → chức năng)

Tất cả migration dưới đây ship trong **`e55247f`**. Manifest pilot: `deploy/ubuntu/migration-files.family-os.txt` — **không** gộp vào `migration-files.prod.txt` (Pharmacy prod).

| Mig | Chức năng | Ghi chú |
|-----|-----------|---------|
| 192–201 | Nền pack (`pack_family`), agreements, routines, team unlock, push, … | Pilot schema |
| 212 | Biết ơn trẻ (`child_gratitude`) | Gratitude flow parent/kid |
| 213 | Hoàn thành sớm (`allow_early_complete`) | Nền cho early lead |
| **214** | **Sổ sao thật + late tiers** | `FamilyStarCalculator`, ledger DB |
| **215** | **Cài đặt sao admin** | `FamilyOsStarSettingsPage`, tier T1/T2/T3 |
| **216** | **Early lead + on-time grace** | `early_lead_minutes`, `on_time_grace_minutes` per commitment |
| **217** | **Pending stars + phụ huynh duyệt** | Sao chờ parent approve trước khi post |
| **218** | **Kho thưởng + đổi sao** | Reward catalog, redeem trên `family-app` |
| **219** | **Nhật ký cảm xúc** | `member_mood_entry`, mood trên board |
| **220** | **Admin CRUD kho thưởng** | `FamilyOsRewardsPage` — thêm/sửa/ẩn catalog |

**UI đã ship cùng commit:**

- **family-app** (`client/family-app/`): Kid focus, Parent board, onboarding, unlock PIN — **mock cleanup**: gọi API thật, không hardcode demo state.
- **Admin** (`client/admin/src/modules/family-os/`): Overview, Day flow, Members, Routines, Agreements, **Kho thưởng**, **Cài đặt sao**, vertical-aware RBAC.
- **Deploy pilot:** `family.kittech.vn` — nginx site riêng; API proxy qua `api.novixa.vn`; script `deploy/ubuntu/apply-family-os-pilot.sh`.

---

## 3. Bug đã sửa trong epic (`e55247f`)

| Triệu chứng | Nguyên nhân gốc | Khóa (file / pattern) |
|-------------|-----------------|------------------------|
| **Kiểm tra** không ưu tiên trước **Nhắc ngay** | Attention list gộp overdue trước awaiting | `ParentBoardView.tsx`: loop `awaiting_check` **trước** loop overdue trong `attentionItems` |
| Nút **Nhắc ngay** / share lỗi im lặng | `navigator.share` AbortError hoặc thiếu clipboard | `nudge.ts` `shareOrCopyNudge`: bắt `AbortError`; fallback `clipboard` / `execCommand` |
| Team-unlocks API 500 / lọc ngày sai | SQL cast date khi param null | `FamilyTeamUnlockRepository.ListAsync`: `(@FlowDate::date IS NULL OR flow_date = @FlowDate::date)` |
| **Kho thưởng** admin trắng khi team-unlock lỗi | `Promise.all` — một reject làm fail cả trang | `FamilyOsRewardsPage.load`: catalog trước; `fetchTeamUnlocks` trong try/catch riêng |
| **Ẩn** phần thưởng không mở confirm | Ant Dropdown + Modal cùng focus | `confirmHideReward`: `setTimeout(0)` trước `modal.confirm` |
| Label muộn `515′` khó đọc | Phút raw từ API | `late-duration.ts` + `FamilyStarCalculator.FormatLateDuration` → `"8 giờ 35 phút"` |
| Vườn sao không **wilted** khi âm sao | UI luôn healthy | `KidFocusView.tsx`: `gardenPlantMood(stars < 0 → wilted)` + emoji héo |
| **Đúng giờ** hiện 0⭐ | Tier/format label sai delta | `FamilyStarCalculator`: `lateMinutes <= 0` → `OnTimeResult(reward)` full stars |

---

## 4. Deploy Family OS (pilot)

### 4.1 Không nhầm manifest

| Manifest | Dùng khi |
|----------|----------|
| `deploy/ubuntu/migration-files.prod.txt` | Pharmacy prod (`novixa.vn`) |
| `deploy/ubuntu/migration-files.family-os.txt` | **Family OS pilot only** — 192–220 |

Chạy mig Family OS:

```bash
bash /opt/kit-platform/run-family-os-migrations-prod.sh "$CONN"
# hoặc qua apply-family-os-pilot.sh (tự gọi script trên)
```

### 4.2 CRLF pitfall (Windows → VPS)

Manifest checkout trên Windows có thể có `\r` cuối dòng → psql tìm file `214_pack_family_star_ledger.sql\r` → **fail**.

**Fix:** `b76f547` — `run-family-os-migrations-prod.sh` strip CR: `line="${line//$'\r'/}"`.

Luôn pull tip có `b76f547` trước khi chạy mig trên VPS.

### 4.3 Trình tự deploy app (tóm tắt)

```text
1. git fetch origin && git rev-parse HEAD origin/main   # local = remote tip
2. Build local (bắt buộc trước push):
   cd client/admin && npm run build          # tsc — TS6133 chặn prod
   cd client/family-app && npm run build
   dotnet publish src/KitPlatform.Api/...    # nếu đổi API/pack
3. git commit + git push origin main
4. scripts/deploy-production.ps1 (hoặc upload bundle lên VPS)
5. Trên VPS: bash apply-family-os-pilot.sh
   → rsync api + family-app + admin (optional) + run-family-os-migrations-prod.sh
6. Verify:
   - https://api.novixa.vn/api/health (+ /api/health/db)
   - https://family.kittech.vn (SPA + /api proxy)
   - psql: SELECT count(*) FROM information_schema.tables WHERE table_schema='pack_family';
7. Hard refresh admin + family-app (Ctrl+F5)
```

**Docs-only commit:** push git đủ; **không** redeploy VPS trừ khi chỉ sửa script deploy/migration trên server.

---

## 5. Checklist đồng bộ — bắt buộc trước khi rời máy / epic mới

Chạy checklist này để tránh lặp lại sáng 2026-07-24 (POS fix không lên VPS, Pharmacy RBAC regress, Family OS mig fail CRLF).

### 5.1 Git / SoT

- [ ] `git fetch origin`
- [ ] `git rev-parse HEAD` **==** `git rev-parse origin/main` (hoặc branch feature đã push, không dirty mơ hồ)
- [ ] `git status -sb` — **không** còn uncommitted: auth/RBAC, Family OS, Pharmacy POS fix
- [ ] Stash/WIP có nhãn rõ (`git stash push -m "WIP family-os …"`) — **không** coi VPS là SoT

### 5.2 Build local (trước deploy)

- [ ] `cd client/admin && npm run build` — pass (`tsc --noEmit`)
- [ ] `cd client/family-app && npm run build` — pass
- [ ] `dotnet build KitPlatform.slnx` (hoặc publish API) — pass nếu đổi backend

### 5.3 Deploy

- [ ] `git push origin main` **trước** upload/deploy VPS
- [ ] Pharmacy: `deploy-production.ps1` + `deploy-update-vps.ps1` (+ `-RunMigrations` nếu mig prod mới)
- [ ] Family OS: manifest **`migration-files.family-os.txt`**, không prod manifest
- [ ] Script mig trên VPS ≥ `b76f547` (CRLF strip)

### 5.4 Verify VPS

- [ ] `https://api.novixa.vn/api/health` OK
- [ ] `https://family.kittech.vn` load SPA; login flow smoke
- [ ] `pack_family` tables tồn tại (count > 0 sau mig)
- [ ] Admin Family OS: Overview + Kho thưởng + Cài đặt sao mở được
- [ ] Pharmacy smoke STAFF: không KPI doanh thu / không `/reports` — xem [pharmacy-rbac-deploy-sync-runbook-v1.md](./pharmacy-rbac-deploy-sync-runbook-v1.md) §4

### 5.5 Sau deploy SPA

- [ ] Hard refresh admin (`Ctrl+F5`) — bundle cũ hay gây “fix không thấy”
- [ ] Hard refresh family-app nếu user test pilot

---

## 6. File đỏ (Family OS)

| Khu vực | Path |
|--------|------|
| Kid / Parent UI | `client/family-app/src/modules/flow/KidFocusView.tsx`, `ParentBoardView.tsx` |
| Nudge + share | `client/family-app/src/shared/nudge/nudge.ts`, `QuickNudgeButton.tsx` |
| Late label | `client/family-app/src/shared/flow/late-duration.ts` |
| Star calc | `src/Packs/FamilyOS/.../FamilyStarCalculator.cs` |
| Team unlock SQL | `FamilyTeamUnlockRepository.cs` |
| Admin kho thưởng | `client/admin/src/modules/family-os/FamilyOsRewardsPage.tsx` |
| Admin cài đặt sao | `client/admin/src/modules/family-os/FamilyOsStarSettingsPage.tsx` |
| Mig manifest pilot | `deploy/ubuntu/migration-files.family-os.txt` |
| Mig runner | `deploy/ubuntu/run-family-os-migrations-prod.sh` |
| Pilot deploy | `deploy/ubuntu/apply-family-os-pilot.sh` |
| Platform chung (đụng là regress Pharmacy) | `router.tsx`, `AppLayout.tsx`, `usePermission.ts` — xem runbook Pharmacy |

---

## 7. Liên quan

- Pharmacy RBAC + deploy: [pharmacy-rbac-deploy-sync-runbook-v1.md](./pharmacy-rbac-deploy-sync-runbook-v1.md)
- POS/KH/Learning sáng: [pharmacy-pos-customer-incidents-v1.md](./pharmacy-pos-customer-incidents-v1.md)
- Smoke Family OS: [family-os-smoke-checklist-v1.md](../03-solution/family-os-smoke-checklist-v1.md)
- Cursor: `.cursor/rules/family-os-deploy-sync.mdc`, `.cursor/rules/pharmacy-rbac-deploy-sync.mdc`
