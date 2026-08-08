# Famixa · School Schedule API Spec v1 (nháp nhanh)

**Mã:** KIT-PRD-FO-SCH-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-08-08 · **Trạng thái:** Spec draft · client P0 đã ship localStorage  
**Phụ thuộc:** P0 `client/family-app/src/shared/school/school-season.ts` · Blueprint layers (`patchFamilyBlueprintLayers`) · [famixa-self-calibration-playbook-v1.md](./famixa-self-calibration-playbook-v1.md)

---

## 1. Mục tiêu

Persist **lịch học từng con** (giờ im lặng / tan học / học thêm) để:

- Đồng bộ đa thiết bị (không mất khi xóa localStorage)
- Server có thể **suppress parent push** trong school bubble (P1)
- Day-flow / coach đọc giờ tan học để neo cửa sổ tối

**Không** nhầm với `context.school.code` (CAL-01 = bubble cạnh tranh trường). Đó là **môi trường học thuật**, không phải đồng hồ.

---

## 2. Nguyên tắc thiết kế

| | |
|---|---|
| **SoT v1** | `pack_family.family_blueprint.layers_json` — sparse, không bảng riêng |
| **Phạm vi** | Theo `membershipId` (role child) |
| **API bề mặt** | Tái dùng `PUT/GET …/blueprint` + client deep-merge (như praiseStyle) |
| **Optional P1** | Endpoint đọc tiện lợi cho reminder worker / push |
| **Non-goal v1** | Lịch từng môn, tiết học, đồng bộ Google Calendar |

---

## 3. JSON shape (layers)

### 3.1 Path

```
layers.members.<memberId>.schoolSchedule
```

Cùng namespace `members.<id>` với `praiseStyle` — merge không đè lẫn nếu patch đúng key.

### 3.2 Schema

```ts
type Weekday = 1 | 2 | 3 | 4 | 5 | 6 | 7; // Mon=1 … Sun=7

type SchoolDayMode = 'off' | 'morning' | 'full';

type SchoolScheduleV1 = {
  schemaVersion: 1;
  /** false = hè / nghỉ mùa học — không quiet */
  seasonOn: boolean;
  mode: SchoolDayMode;
  /** Mặc định [1,2,3,4,5] */
  weekdays: Weekday[];
  /** HH:mm local family TZ (Asia/Ho_Chi_Minh) */
  schoolStart: string;
  schoolEnd: string;
  hasExtraClass: boolean;
  /** Bắt buộc nếu hasExtraClass; = quiet end khi mode=full */
  extraEnd?: string;
  source: 'parent_settings' | 'onboarding_seed' | 'migrated_local';
  updatedAt: string; // ISO-8601
  updatedByMemberId?: string;
};
```

### 3.3 Ví dụ patch

```http
PUT /api/family-os/families/{familyId}/blueprint
Content-Type: application/json
```

Body (qua `patchFamilyBlueprintLayers` — merge rồi `replace: true`):

```json
{
  "members": {
    "019xxxxx-child-uuid": {
      "schoolSchedule": {
        "schemaVersion": 1,
        "seasonOn": true,
        "mode": "full",
        "weekdays": [1, 2, 3, 4, 5],
        "schoolStart": "07:00",
        "schoolEnd": "16:30",
        "hasExtraClass": true,
        "extraEnd": "18:30",
        "source": "parent_settings",
        "updatedAt": "2026-08-08T08:00:00+07:00",
        "updatedByMemberId": "019xxxxx-parent-uuid"
      }
    }
  }
}
```

### 3.4 Validation (server hoặc client cứng trước PUT)

| Rule | Lỗi |
|------|-----|
| `memberId` thuộc family + `roleCode=child` | `400 MEMBER_NOT_CHILD` |
| `schoolStart` / `schoolEnd` / `extraEnd` match `^([01]\d\|2[0-3]):[0-5]\d$` | `400 INVALID_TIME` |
| `schoolEnd` > `schoolStart` (cùng ngày) | `400 WINDOW_ORDER` |
| `hasExtraClass` ⇒ `extraEnd` ≥ `schoolEnd` | `400 EXTRA_END` |
| `weekdays` ⊂ 1..7, length ≥ 1 khi `seasonOn && mode≠off` | `400 WEEKDAYS` |
| `mode=off` hoặc `seasonOn=false` ⇒ bỏ qua kiểm tra cửa sổ giờ | OK |

Timezone: **family TZ cố định VN** v1 (đã có trên Settings). Không gửi offset per field.

---

## 4. API bề mặt

### 4.1 V1 ship (không endpoint mới)

| Method | Path | Dùng cho |
|--------|------|----------|
| `GET` | `/family-os/families/{familyId}/blueprint` | Hydrate SPA khi mở Settings / day-flow |
| `PUT` | `/family-os/families/{familyId}/blueprint` | Persist patch đã merge |

Client:

1. `resolveSchoolSchedule` đọc local → nếu có blueprint, **prefer server** nếu `updatedAt` mới hơn  
2. Save Settings: `saveSchoolSchedule` local + `patchFamilyBlueprintLayers(memberSchoolSchedulePatch(...))`  
3. Migrate 1 lần: local → server với `source: migrated_local`

### 4.2 P1 (optional) — helper cho push/reminder

```http
GET /family-os/families/{familyId}/members/{memberId}/school-schedule
```

**200**

```json
{
  "memberId": "…",
  "schedule": { /* SchoolScheduleV1 | null */ },
  "derived": {
    "phase": "at_school",
    "quietNow": true,
    "quietEnd": "18:30",
    "asOf": "2026-08-08T14:22:00+07:00",
    "timeZone": "Asia/Ho_Chi_Minh"
  }
}
```

`phase` / `quietNow` SoT với logic client: `school-season.ts` → port sang `FamilySchoolSchedule` (C#) để worker dùng chung.

```http
GET /family-os/families/{familyId}/school-schedule/quiet-map?asOf=…
```

Trả map `memberId → quietNow` cho batch parent-push filter.

---

## 5. Hành vi runtime (contract)

| Phase | Kid notify/chime | Parent hot push (P1) | UI |
|-------|------------------|----------------------|-----|
| `before_school` | Cho phép (nhẹ) | Cho phép | “Sáng vội OK” |
| `at_school` | **Tắt** | **Tắt** (P1) | Quiet banner |
| `after_school` | Cho phép | Hạ budget / 1 hero | Landing tan học |
| `evening` | Cho phép | Prefer `evening_fatigue` tactics | 1 hero task |
| `weekend` / `season_off` | Bình thường | Bình thường | Không banner mùa học |

**Quiet end** = `extraEnd` nếu `hasExtraClass && mode=full`, else `schoolEnd`; `mode=morning` quiet end = `schoolEnd`.

Overdue việc sáng trong/after school: UI soft *“Chưa ghi nhận”* — **không** đổi `reminderState` server v1 (chỉ presentation). P2 có thể thêm `softOverdueEligible` flag.

---

## 6. Auth / RBAC

| Ai | Đọc | Ghi |
|----|-----|-----|
| Parent / guardian (family member) | ✓ | ✓ |
| Child | ✓ own schedule (hydrate) | ✗ (chỉ parent Settings) |
| Platform admin | ✓ tenant | không qua SPA này |

Reuse entitlement Family OS hiện có trên blueprint routes.

---

## 7. Migration từ P0 local

Key local: `famixa.school-season.v1` = `Record<memberId, ScheduleLocal>`.

| Bước | |
|------|--|
| 1 | Login parent → GET blueprint |
| 2 | Với mỗi child: nếu local có & layers thiếu → PUT migrate |
| 3 | Nếu cả hai có → lấy `updatedAt` mới hơn |
| 4 | Sau migrate thành công: giữ local mirror (offline) |

Không xóa local ngay — mirror write-through.

---

## 8. Client helpers (đề xuất)

```ts
// soft-calibration.ts hoặc school-season.ts
export function memberSchoolSchedulePatch(
  memberId: string,
  schedule: SchoolScheduleV1,
): Record<string, unknown>

export function readMemberSchoolSchedule(
  layers: Record<string, unknown>,
  memberId: string,
): SchoolScheduleV1 | null
```

`resolveSchoolSchedule(memberId, familyId)`:

1. layers (server cache / fetch)  
2. else local  
3. else `defaultScheduleForAge` + optional persist local  

---

## 9. Test plan (API)

- [ ] PUT schedule child A không xóa `praiseStyle` child A / schedule child B  
- [ ] GET sau PUT round-trip fields  
- [ ] Validation `extraEnd < schoolEnd` → 400  
- [ ] Child token PUT → 403  
- [ ] `quiet-map` (P1): 10:00 weekday → `quietNow=true`; 19:00 → false  
- [ ] Migrate local → server; máy 2 hydrate đúng  

---

## 10. Sequencing

| Wave | Việc |
|------|------|
| **SCH-01a** | Spec này + client write-through blueprint (không endpoint mới) — done |
| **SCH-01b** | C# `FamilySchoolSchedule` derive phase; unit test parity với `check-school-season.mjs` — done (`FamilySchoolSchedule.cs` + `FamilySchoolScheduleTests`) |
| **SCH-01c** | Parent-push / reminder suppress dùng `quietNow` — done (`DispatchHotCommitments` + day-flow `AllowParentPush`/`AllowChildChime` via `SchoolQuiet`) |
| **SCH-02** | GET helper + admin visibility; day-flow `schoolPhase` / `schoolMembers` — done |

---

## 11. Open questions

1. Học bán trú tan 16:30 nhưng ở trường tới 17:30 — có cần `stayAtSchoolUntil` tách `schoolEnd`? (v1: gộp vào `extraEnd` / kéo `schoolEnd`)  
2. Thứ 7 học thêm — `weekdays` đã cover; UI Settings có cần chip T7?  
3. Có đưa `schoolPhase` vào `DayFlow` response không, hay chỉ client derive?

**Đề xuất mặc định:** (1) gộp v1 · (2) chip T7 trong Settings · (3) client derive tới khi push cần server.

---

*Owner: FamilyOS / Famixa · Review trước SCH-01a implement.*
