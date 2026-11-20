# Episode Workspace V2-F — Runtime Smoke Test

**Date:** 2026-09-27  
**Tester:** Agent (read-only, no code/data changes)  
**Environment:** Local `KIT_MKT` / admin @ `http://localhost:5173`  
**Mode:** RUNTIME VERIFICATION ONLY

---

## Executive Summary

| Verdict | Details |
|---------|---------|
| **PASS WITH GAPS** | Workspace opens, lineage, story, variants, canon lock, previous/next, and empty-content state behave as specified. Asset thumbnail fails to load (404 on file endpoint). No content was created, approved, or published. |

---

## Test Coverage

### A. Episodes Tested

| Series | Episode | Status | Content |
|--------|---------|--------|---------|
| `NOVIXA-S002` — Hàng quá hạn là tiền nằm trong kho | E01 `01a0e0b4-e494-7820-8288-770134d73272` | Content Review | 5 variants, no assets |
| same | E02 `01a0e0b4-e49d-7734-9bd3-e975639bb450` | Content Review | 5 variants, no assets |
| `NOVIXA-S001` — Việc nhỏ trong quản lý kho quyết định số phận nhà thuốc | E01 `01a0aadb-3c6a-7923-ae72-85ff273b77c2` | **Approved**, scheduled | 5 variants, 1 asset (selected) |
| same | E02 `01a0aadb-3c72-7f12-b0ff-a59d5541d65e` | **PLANNED** | No content |

Route: `/content/article-series/:seriesId/episodes/:episodeId`

---

## B. Checklist Results

| # | Item | Result | Notes |
|---|------|--------|-------|
| 1 | Series → Episode navigation | ✅ PASS | Episode workspace opens successfully. No blank page, no runtime exception. |
| 2 | Header / Lineage | ✅ PASS | Brand → Brand Angle → Series → Episode. Brand link navigates to `/content/brand-view` (not `/content/brands`). |
| 3 | Story Control | ✅ PASS | Sequence, Title, Objective, Angle, Key Message, Brand Angle, Series, Audience visible. Continuity and Creative Brief blocks render. |
| 4 | Content State | ✅ PASS | Content status, Quality Gate visible. "Xem bài" available. Schedule date shown on Approved episode: `Lịch đăng: 22:43:29 16/9/2026`. |
| 5 | Variants | ✅ PASS | Five variants render with type (Facebook, Facebook ngắn, SEO, Caption, Website), localized label, and body preview. No console/runtime error. |
| 6 | Assets | ❌ FAIL | Asset section renders filename `01a0aadd7f897cf1ae9e6561128e9fbf.png` and selected state "Đang dùng". **Thumbnail is empty gray box** due to 404 on `/api/content/assets/{id}/file`. No broken `<img>` icon. |
| 7 | Canon Lock | ✅ PASS | Approved episode shows "Episode đã được duyệt. Canon đã khóa." "Sửa episode" and Regenerate are absent. Content not changed. |
| 8 | Previous / Next | ✅ PASS | E01 ↔ E02 navigation preserves Series/Episode route correctly. |
| 9 | No-Content Case | ✅ PASS | E02 without content shows "Episode chưa có nội dung" and "Tạo bài". Button was not clicked. |
| 10 | Approval / Publishing | ⏭️ SKIPPED (execution) | Controls match status: Review (Regenerate, Duyệt, Xem bài), Approved (Đặt lịch / Xuất bản, Xem bài). No approve, schedule, or publish was run. |
| 11 | Console | ⚠️ FAIL (warning) | `Warning: Instance created by useForm is not connected to any Form element` on Review episode with "Sửa episode" collapsed. Page still works. |
| 12 | Network | ❌ FAIL | `404 GET /api/content/assets/01a0aadd-7f89-7cf1-ae9e-6561128e9fbf/file` (twice). Other episode requests returned 200. |

---

## C. Issues Found

### V2-F Related

1. **Asset thumbnail does not load (404)**  
   - **Severity:** Medium  
   - **Impact:** Approved episode asset preview shows gray placeholder instead of image thumbnail  
   - **Root cause:** File endpoint returns 404 when stored file is not on disk  
   - **Data:** Asset row exists, `isSelected: true`, filename present  
   - **Network:** `GET /api/content/assets/01a0aadd-7f89-7cf1-ae9e-6561128e9fbf/file` → 404  
   - **Workaround:** Workspace catches 404 and renders gray placeholder; no broken image icon  

2. **`useForm` console warning**  
   - **Severity:** Low (cosmetic)  
   - **Impact:** Console warning on episodes with "Sửa episode" collapsed  
   - **Message:** `Warning: Instance created by useForm is not connected to any Form element. Forget to pass form prop?`  
   - **Does not block:** Page renders and functions correctly  

### Pre-existing (unrelated to V2-F)

1. **Ant Design React 19 compatibility warning** (app-wide):  
   `Warning: [antd: compatible] antd v5 support React is 16 ~ 18.`

2. **Brand-view navigation**  
   - Clicking "Novixa" in lineage opens `/content/brand-view` showing "Nhà thuốc Xuân Hòa" (the current brand in the view), not Novixa  
   - Route is correct (`/content/brand-view`), but content displayed does not match lineage brand  

---

## D. Screenshots

Screenshots saved to `C:\Users\Admin\AppData\Local\Temp\cursor\screenshots\`:

- `series-list.png` — Series list page
- `series-detail.png` — Series detail with episode timeline
- `episode-approved.png` — Approved episode (E01, NOVIXA-S001) with canon lock, schedule date, "Đặt lịch / Xuất bản"
- `episode-asset.png` — Asset section showing gray placeholder (404 on file)
- `episode-nocontent.png` — Episode without content (E02, NOVIXA-S001) showing "Episode chưa có nội dung" and "Tạo bài"

---

## E. Test Environment

- **Runtime:** Local admin `http://localhost:5173`, API `http://localhost:5290`
- **Tenant:** `KIT_MKT`
- **User:** `admin` (ADMIN)
- **Session:** Already signed in (no login flow tested)
- **Data:** No files, content, approvals, or publish actions were changed

---

## F. Final Verdict

**PASS WITH GAPS**

The Episode Workspace V2-F implementation successfully renders lineage, story control, variants, canon lock, previous/next navigation, and the empty-content state. The two gaps are:

1. **Asset thumbnail 404** (medium severity): selected asset renders filename and "Đang dùng" but preview stays blank due to missing file on disk  
2. **`useForm` warning** (low severity, cosmetic): console warning when edit form is not mounted

No content was created, approved, or published during this test.

---

**Next Steps:**

- Investigate asset file storage path or seed missing asset file for `01a0aadd-7f89-7cf1-ae9e-6561128e9fbf.png`
- Conditionally mount `useForm` only when "Sửa episode" collapse is expanded (or suppress warning)
- (Optional) Fix brand-view to accept brand ID from lineage trail
