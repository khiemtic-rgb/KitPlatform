# V2-G — Content Workspace READ-ONLY AUDIT

**Date:** 2026-09-27  
**Mode:** READ-ONLY (no code/architecture changes)  
**Context:** V2-F Episode Workspace is CLOSED. Defining V2-G scope.

---

## A. CURRENT CONTENT PAGE

**File:** `client/admin/src/modules/content/ContentTopicsPage.tsx` (2,432 lines)

### Structure

**List View:**
- Table with brand filter, core filter (ý tưởng gốc), batch selection
- Columns: Thương hiệu, Tiêu đề (with Series/Episode line if exists), Bản viết count, Ngày hiển thị, Trạng thái, Việc cần làm
- Row actions: "AI viết + ảnh", "Xem / duyệt", "Sửa", "Xóa"
- Batch actions: "Duyệt đã chọn", "Xuất bản đã chọn"
- Create actions: "Nhập Excel", "Thêm 1 bài", "Dán nhiều tiêu đề"

**Detail View (Drawer 1040px):**
- Opens with `setDetailOpen(true)` + `loadDetail(topicId)`
- Tabs:
  1. **Bản viết** — list of variants (kind + title + body markdown ellipsis)
  2. **Ảnh** — local folder gallery + AI assets on server (select, upload, generate)
  3. **Đăng tay** — `ContentManualPostTab` (copy image, manual export)
  4. **Xuất bản** — jobs table with status, error formatting, retry actions

**Actions in Drawer Extra:**
- "AI viết + ảnh", "Chỉ viết chữ", "Tạo ảnh"
- "Quay lại Episode" (if Series content)
- "Regenerate từ Episode" (if Series + not approved)
- "Duyệt", "Đẩy lịch đăng" / "Xuất bản"

---

## B. EXISTING DATA CONTRACTS

**Core Types** (`client/admin/src/shared/api/content.api.ts`):

```typescript
ContentTopic {
  id, brandId, brandCode, brandName,
  title, pillar, goal, ctaUrl, utmCampaign,
  priority, status, bodyOutline, displayAt,
  variantCount, corePackageId, coreTitle
}

ContentTopicDetail {
  topic: ContentTopic,
  variants: ContentVariant[],
  assets: ContentAsset[],
  jobs: ContentPublishJob[]
}

ContentVariant {
  id, topicId, kind, title, bodyMarkdown, metaJson
}

ContentAsset {
  id, topicId, kind, fileName, contentType,
  prompt, model, imageTier, estimateUsd, isSelected
}

ContentQualityGate {
  passed, issues, checkedAt,
  blockingIssues, approveIssues,
  canPublish, canApprove
}

ContentPublishJob {
  id, topicId, brandId, targetKind,
  siteTargetId, channelTargetId, connectorType,
  status, publishAt, externalRef, lastError, resultJson
}

ContentPackage {
  id, brandId, topicId, title, angle, audience,
  contentType, pillar, goal, priority, status,
  displayAt, variantCount,
  qualityGate: ContentQualityGate | null,
  creativeBrief: ContentCreativeBrief | null
}
```

**No `ContentWorkJob` type found.** (Publish jobs are the work jobs.)

---

## C. EXISTING ACTIONS

**APIs** (`content.api.ts`):

1. **List/Read:**
   - `fetchContentTopics({ brandId?, status? })` → `ContentTopic[]`
   - `fetchContentTopicDetail(id)` → `ContentTopicDetail`
   - `fetchContentPackages({ brandId })` → `ContentPackage[]`
   - `fetchContentAssetObjectUrl(assetId)` → `Promise<string>` (blob URL)

2. **Create/Update:**
   - `createContentTopic({ brandId, title, pillar, goal, ... })` → `ContentTopic`
   - `updateContentTopic(id, { ... })` → `ContentTopic`
   - `deleteContentTopic(id)`

3. **Generate:**
   - `generateContentTopic(id, { skipImages?, imagesOnly? })` → `{ message }`
   - `generateContentArticleEpisode(seriesId, episodeId)` → `{ message }` (Series regenerate)

4. **Approve:**
   - `approveContentTopic(id)` → `ContentTopic`

5. **Publish:**
   - `publishContentTopic(id, { includeManualExport, runImmediately, publishAt, imageBlob?, imageFileName?, imageContentType? })` → `{ jobs: ContentPublishJob[] }`
   - `runContentPublishJob(jobId, { imageBlob?, imageFileName?, publishAt? })` → `ContentPublishJob`

6. **Assets:**
   - `selectContentAsset(topicId, assetId)`
   - `uploadContentTopicAsset(topicId, file: File)` → `ContentAsset`

**All necessary APIs exist.** No missing API for V2-G.

---

## D. EXISTING LINEAGE

**Helpers** (`content-lineage.tsx`):

```typescript
// Index: Map<topicId, { series: ContentArticleSeries, episode: ContentArticleEpisode }>
useTopicLineageIndex()  // Hook builds lineage from fetchContentArticleSeries

// Trail builders
seriesContentTrail(lineage, contentTitle) → [
  Brand → Brand Angle → Series Code → Episode → Content Title
]

standaloneContentTrail(brandName, brandAngle?) → [
  Brand → Brand Angle? → "Bài độc lập"
]

episodeDetailTrail(series, episode) → [
  Brand → Brand Angle → Series → Episode
]

seriesDetailTrail(series) → [
  Idea Pool → Brand Angle → Series Code
]

// Current identification logic:
- Series content: lineageIndex.has(topicId)
- Standalone: !lineageIndex.has(topicId)
- Canon lock check: lineageIndex.has(topicId) && contentStatusApproved(status)
```

**Navigation:**
- **Content → Episode:** Link in drawer: `/content/article-series/{seriesId}/episodes/{episodeId}` ("Quay lại Episode")
- **Content → Series:** Link in drawer: `/content/article-series/{seriesId}` ("Quay lại Series")
- **Brand link:** `/content/brand-view` (currently shows different brand — known V2-F gap)
- **Brand Angle link:** `/content/packages?package={packageId}`

**Quality Gate Display:**
- `qualityGateLabel(gate)` formats gate.passed / issues
- Used in: Episode workspace, Content drawer "Nguồn bài" card

---

## E. CONCRETE UX GAPS

### 1. **Drawer is Not a Dedicated Page**
- Current: 1040px drawer over list
- Gap: Cannot bookmark Content detail URL
- Gap: Browser back from drawer goes to list, loses Content context
- Gap: Cannot open Content in new tab
- Gap: Drawer shares URL with list (`?topic={id}` param on `/content/topics`)

### 2. **No Persistent Content Workspace Route**
- Current: Drawer opens via `searchParams.get('topic')` or row click
- Gap: No `/content/topics/{id}` dedicated page
- Gap: Cannot share direct link to Content workspace
- Gap: Cannot cmd+click to open Content in background tab

### 3. **Content Status at Top is Tiny**
- Current: Single `<StatusTag>` + brand name + paragraph hint
- Gap: Status, Quality Gate, Schedule date are buried in prose
- Gap: No visual summary card for Content state (like Episode "Story / Canon" card)
- Gap: Lineage + QG info inside "Nguồn bài" card is small, collapsible

### 4. **Variants Preview Lacks Structure**
- Current: Flat list with `ellipsis={{ rows: 4 }}`
- Gap: No preview of first ~100 chars per variant for quick scanning
- Gap: No visual separation between variants
- Gap: Expand/collapse is per-variant but hard to scan

### 5. **Regenerate from Episode is Buried**
- Current: Button in drawer extra, only shows if `topicLineage && !contentStatusApproved`
- Gap: Not explained why Regenerate is disabled for approved Series content
- Gap: Standalone content has "AI viết + ảnh" but no "Regenerate" terminology

### 6. **Jobs Table is Noisy**
- Current: Full table with 4 columns, error formatting inline, retry buttons
- Gap: Hard to see "all succeeded" vs "1 failed" at a glance
- Gap: Job detail (media hint, path) is mixed with error
- Gap: Retry actions are row-by-row, not batched

### 7. **No Content Breadcrumb in List**
- Current: Lineage only shows in drawer
- Gap: List table shows Series code + episode inline but not clickable trail
- Gap: User cannot navigate Brand → Angle → Series → Episode from list row

### 8. **Asset Preview Mixing Local + Server**
- Current: Two separate sections if local folder exists, one section if not
- Gap: Confusing which asset is "selected" when both local + server assets exist
- Gap: "Đang dùng" label on local preview is misleading (only selected locally, not persisted until publish)

---

## F. REUSABLE COMPONENTS/HELPERS

**From V2-F Episode Workspace:**

1. **`ContentLineageTrail`** — already used in Content drawer ✅
2. **`ContentKindTag`** — already used in Content list/drawer ✅
3. **`ContentCanonFields`** — **NOT used in Content drawer** (could be reused for structured state display)
4. **`ContentEpisodeContinuity`** — N/A (Content has no continuity)
5. **`EpisodeCreativeBrief`** — **NOT used** (but `ContentPackage.creativeBrief` exists)
6. **`qualityGateLabel`** — already used ✅
7. **`variantKindLabel`** — already used ✅
8. **`packageForTopic`** — already used ✅
9. **`contentStatusApproved`** — already used ✅
10. **`episodeSequenceLabel`** — already used for Series content ✅

**From `ContentTopicsPage.tsx`:**

1. **`StatusTag`** — inline component (2-line)
2. **`formatPublishError`** — 90-line helper for job errors
3. **`parseTitleLines`** — bulk create helper
4. **Local image library helpers** — 10+ functions (ranking, matching, permission)
5. **`useTopicLineageIndex`** — hook for Series/Episode map
6. **`rewriteBlocked`** — Series canon lock check

**From `content-lineage.tsx`:**

1. **`seriesContentTrail`** / **`standaloneContentTrail`** — trail builders ✅
2. **`usePackagesForBrand`** — fetch packages for brand ✅

---

## G. SMALLEST LIKELY FILE SCOPE

**If creating a dedicated Content Workspace page:**

### New Files (1):
1. **`client/admin/src/modules/content/ContentTopicWorkspacePage.tsx`**
   - Route: `/content/topics/:id`
   - Full-page layout (not drawer)
   - Reuse: lineage trail, status card, variants, assets, jobs from current drawer
   - Add: persistent URL, breadcrumb nav, structured state summary

### Modified Files (2):
1. **`client/admin/src/app/router.tsx`**
   - Add route: `<Route path="topics/:id" element={<ContentTopicWorkspacePage />} />`

2. **`client/admin/src/modules/content/ContentTopicsPage.tsx`**
   - Change `openDetail(row)` → `navigate(/content/topics/${row.id})`
   - Remove drawer (or keep for inline quick view if user prefers)
   - OR: keep drawer for quick preview, add "Open in full page" button

### Reused Files (no changes needed):
- `content-lineage.tsx` — all helpers work as-is ✅
- `ContentManualPostTab.tsx` — already imported ✅
- `content.api.ts` — all APIs exist ✅

**Total new code:** ~400-600 lines (copying drawer structure to full page, adding breadcrumb, structured summary)

---

## H. MISSING API

**None.**

All necessary APIs exist:
- `fetchContentTopicDetail` ✅
- `generateContentTopic` ✅
- `generateContentArticleEpisode` (for Series regenerate) ✅
- `approveContentTopic` ✅
- `publishContentTopic` ✅
- `runContentPublishJob` (retry) ✅
- `selectContentAsset` / `uploadContentTopicAsset` ✅
- `fetchContentAssetObjectUrl` ✅

---

## I. WHAT V2-G MUST NOT CHANGE

### 1. **Existing Content List Page**
- Keep `/content/topics` list functional
- Keep batch actions (approve, publish)
- Keep Excel import, bulk create
- Keep brand/core filters
- Keep local image library integration

### 2. **Existing APIs / Backend**
- Do NOT change `ContentTopic` / `ContentTopicDetail` contracts
- Do NOT change generate/approve/publish endpoints
- Do NOT change job retry logic
- Do NOT change asset storage (404 gap is data/storage issue, not V2-G)

### 3. **Series Content Canon Lock**
- Keep `rewriteBlocked` logic: Series content with approved status cannot regenerate
- Keep "Regenerate từ Episode" flow
- Keep lineage index (`useTopicLineageIndex`)

### 4. **Local Image Library**
- Keep Chrome File System API integration
- Keep local folder permission flow
- Keep image ranking/matching logic
- Keep "Đẩy lịch đăng" with local image blob upload

### 5. **Manual Post Tab**
- Keep `ContentManualPostTab` component
- Keep copy-to-clipboard flows for Nhóm/LinkedIn

### 6. **Job Error Formatting**
- Keep `formatPublishError` logic (GitHub 403/404 hints, WordPress 401, Facebook reconnect)
- Keep retry actions
- Keep Facebook OAuth reconnect flow

### 7. **Batch Publish Flow**
- Keep local folder requirement for batch publish
- Keep per-topic image matching in batch
- Keep auto-approve before publish

---

## SUMMARY

**Current State:**
- Content page is a **list + 1040px drawer**
- All APIs exist ✅
- Lineage helpers work ✅
- Assets, variants, jobs render correctly ✅

**Concrete Gaps:**
1. Drawer is not a bookmarkable page
2. Content state summary is buried in prose
3. Variants preview is flat text ellipsis
4. Jobs table is noisy
5. Regenerate from Episode is not explained
6. No breadcrumb nav in list

**Smallest V2-G Scope:**
- Create **`ContentTopicWorkspacePage.tsx`** (full page, not drawer)
- Add route `/content/topics/:id`
- Reuse 90% of current drawer code
- Add structured state summary card (like Episode "Story / Canon")
- Keep list page + drawer for quick preview (optional)

**No backend/API changes required.**

---

**END OF AUDIT**
