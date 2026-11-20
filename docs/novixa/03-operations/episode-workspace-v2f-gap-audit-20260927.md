# V2-F GAP AUDIT — Asset 404 + useForm Warning

**Date:** 2026-09-27  
**Mode:** READ-ONLY (no code/data/storage changes)

---

## A. ASSET 404 ROOT CAUSE

### Affected Asset

```
ID: 01a0aadd-7f89-7cf1-ae9e-6561128e9fbf
Endpoint: GET /api/content/assets/{id}/file
Response: 404 Not Found
```

### Root Cause

**Missing physical file in storage directory.**

#### Flow

1. **Frontend**: `fetchContentAssetObjectUrl(assetId)` → `GET /content/assets/{id}/file`
2. **Backend**: `ContentController.GetAssetFile(id)` → `ContentPublishService.GetAssetFileAsync(id)`
3. **Service logic**:
   ```csharp
   var asset = await _repo.GetAssetAsync(assetId, cancellationToken);
   if (asset is null) return null;
   var path = Path.Combine(ResolveAssetRoot(), asset.StoragePath.Replace('/', Path.DirectorySeparatorChar));
   if (!File.Exists(path)) return null;  // ← Returns 404
   ```

4. **Asset root resolution** (`ContentPublishService.ResolveAssetRoot()`):
   ```csharp
   var configured = string.IsNullOrWhiteSpace(_options.AssetRoot)
       ? "App_Data/content-assets"
       : _options.AssetRoot;
   return Path.IsPathRooted(configured)
       ? configured
       : Path.GetFullPath(Path.Combine(_env.ContentRootPath, configured));
   ```

5. **Runtime resolution**:
   - `_env.ContentRootPath` = `E:\KitPlatform-kitmkt\src\KitPlatform.Api\bin\Debug\net10.0`
   - Expected path = `E:\KitPlatform-kitmkt\src\KitPlatform.Api\bin\Debug\net10.0\App_Data\content-assets\{storagePath}`
   - **Directory does not exist**: `Test-Path` returned `False`

6. **Asset record exists** in `pack_content.assets` with:
   - `id`, `topic_id`, `file_name`, `storage_path`, `is_selected = true`
   - But physical file at resolved path is missing

#### Why Missing

The `App_Data/content-assets` directory is **not created** in the bin output directory. This can happen when:

1. **Test/demo data scenario**: Asset record seeded/migrated but physical file not copied/generated
2. **Storage path mismatch**: Asset record points to a path that was never written
3. **Gitignored storage**: `.gitignore` excludes `src/KitPlatform.Api/App_Data/kit-video-*` but not `content-assets`
4. **Build artifact issue**: Asset files not copied to bin output during build
5. **Dev environment**: Asset was generated in a different environment/machine

Most likely: **test/demo data without corresponding physical files**.

---

## B. ASSET FIX LAYER

| Layer | Required | Notes |
|-------|----------|-------|
| **Frontend** | ❌ No | Frontend correctly handles 404 and shows gray placeholder |
| **Backend** | ❌ No | Backend logic is correct: returns 404 when file missing |
| **Storage** | ✅ **Yes** | Physical files must exist in resolved asset root path |
| **Data** | ⚠️ Maybe | If asset records reference non-existent files, either: (a) regenerate assets, or (b) mark records as deleted |

### Recommended Fix

**Option 1: Regenerate missing assets** (if generation is available)
- Trigger asset generation for affected topics
- Writes new files to correct storage path

**Option 2: Seed physical files** (for demo/test data)
- Copy or create placeholder images
- Place in `{ContentRootPath}/App_Data/content-assets/{storagePath}`
- Ensure directory structure exists

**Option 3: Clean orphaned records**
- Mark asset records as deleted if files are permanently lost
- Frontend already handles missing assets gracefully

### Files That Would Need Modification

**If fixing storage only** (no code changes):
- Create directory: `E:\KitPlatform-kitmkt\src\KitPlatform.Api\bin\Debug\net10.0\App_Data\content-assets\`
- Add asset file(s) at paths matching `pack_content.assets.storage_path`

**If fixing backend** (not recommended, current behavior is correct):
- None

---

## C. useForm WARNING ROOT CAUSE

### Observed Warning

```
Warning: Instance created by `useForm` is not connected to any Form element. 
Forget to pass `form` prop?
```

### Root Cause

**Form instance created unconditionally, but Form component only mounted when Episode is not canon-locked.**

#### Flow

1. **Component mount** (`ContentArticleEpisodePage.tsx:54`):
   ```tsx
   const [form] = Form.useForm();
   ```
   - `useForm()` is called **every render** regardless of episode status

2. **Form component mount** (lines 265-300):
   ```tsx
   {!episodeCanonLocked && (
     <Collapse items={[{
       key: "edit",
       label: "Sửa episode",
       children: (
         <Form form={form} ...>
           {/* form fields */}
         </Form>
       )
     }]} />
   )}
   ```
   - Form component is **conditionally rendered**
   - When `episodeCanonLocked === true` (Approved/Scheduled/Published), Form is not mounted
   - But `form` instance still exists, triggering Ant Design's warning

3. **Warning source**: Ant Design detects a form instance that was created but never connected to a `<Form>` component

### Is This V2-F Introduced?

**No.** Git history shows `const [form] = Form.useForm()` existed in the **previous commit** (`cef7e9b3 feat(marketing): protect series content canon`).

The V2-F implementation (`ContentArticleEpisodePage.tsx`) **inherited** this pattern but did not introduce it.

### Why Warning Appears Now

The canon lock check `episodeCanonLocked` prevents Form from mounting on Approved episodes. During smoke test, we opened an Approved episode, so:
- Form instance created
- Form component not rendered
- Warning logged

---

## D. SMALLEST SAFE FIX

### Option 1: Conditional Form Instance Creation (Recommended)

Move `Form.useForm()` inside a conditional or lazy initialization:

```tsx
const [form] = useState(() => !episodeCanonLocked ? Form.useForm()[0] : undefined);
```

**Pros**: Clean, no warning  
**Cons**: Requires re-mount if canon state changes (rare)

### Option 2: Always Mount Form (Hidden)

Remove the `!episodeCanonLocked` condition but disable/hide Form:

```tsx
<Collapse
  items={[{
    key: "edit",
    label: "Sửa episode",
    disabled: episodeCanonLocked,  // ← Add this
    children: <Form form={form} disabled={episodeCanonLocked}>...
  }]}
/>
```

**Pros**: Form always mounted, no warning  
**Cons**: Slightly more DOM nodes, disabled form still renders (but hidden in collapsed panel)

### Option 3: Suppress Warning (Not Recommended)

Ignore the warning. Frontend still works.

**Pros**: No code change  
**Cons**: Console pollution

---

## E. FILES THAT WOULD NEED MODIFICATION

### Asset 404 Fix (Storage Layer)

**No code changes required.** Only storage/data operations:

1. Create directory structure
2. Place asset files
3. OR regenerate assets via existing generation flow

### useForm Warning Fix (Frontend)

**One file**:

```
client/admin/src/modules/content/ContentArticleEpisodePage.tsx
```

**Change**: Lines 54 + 265-300 (form instance creation and Form mounting logic)

---

## F. WHETHER V2-F CAN BE CLOSED WITHOUT FIXING EACH ISSUE

### Asset 404

**Can close V2-F: ✅ Yes**

- UI gracefully handles 404 (shows gray placeholder, no crash)
- Filename and selected state still render correctly
- Issue is **test/demo data integrity**, not V2-F implementation
- Fix belongs in **data seeding / storage management**, not V2-F feature scope

**Recommendation**: Log as separate **data/devops issue** for asset seeding.

### useForm Warning

**Can close V2-F: ✅ Yes**

- Warning is **cosmetic** (page works correctly)
- Pre-existing pattern (not introduced by V2-F)
- Does not block runtime or user workflows

**Recommendation**: Log as separate **code quality / tech debt** issue for conditional form instance creation. Can be fixed in a future cleanup pass.

---

## SUMMARY

| Issue | Root Cause | Fix Layer | Blocks V2-F? |
|-------|------------|-----------|--------------|
| **Asset 404** | Physical file missing in `App_Data/content-assets/` | Storage / Data | ❌ No |
| **useForm Warning** | Form instance created but not mounted on canon-locked episodes | Frontend (cosmetic) | ❌ No |

**V2-F can be closed.** Both issues are non-blocking:
- Asset 404 is a data/storage issue with graceful UI fallback
- useForm warning is cosmetic and pre-existing

Recommended next steps:
1. Create data/devops ticket for asset seeding
2. Create code quality ticket for conditional form instance
3. Close V2-F with documentation of known gaps

---

**END OF AUDIT**
