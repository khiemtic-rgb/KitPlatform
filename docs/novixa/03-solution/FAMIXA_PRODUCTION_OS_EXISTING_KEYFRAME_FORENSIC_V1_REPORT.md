# FAMIXA PRODUCTION OS — EXISTING PHYSICAL KEYFRAME FORENSIC V1

Date: 2026-09-17  
Official build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Priority shot: `EP01-SC01-SH02`  
Tenant / session: `KIT_MKT` admin, Admin Vite `localhost:5173`

Forensic only. No image generation, no provider calls, no POST `/keyframes` or `/requests`, no Production OS / UI / schema edits, no fake artifact, no copy of another shot’s pixels onto SH02, no graph rewrite of `kfApproved`.

---

## Verdict

**C. No usable existing KF found**

Official SH02 has a graph filename and `kfApproved=true`, but **no physical pixels** on API disk, Production OS storage, series graph JSON, or this browser IndexedDB.

Stop. Do not generate. Do not import. Do not create a fixture in this phase.

---

## 1. Search scope

| Source | Result for official SH02 |
|---|---|
| `E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-kf` | No `EP01-SC01-SH02` folder, no `kf-EP01-SC01-SH02-canon.jpg` |
| `App_Data/kit-video-i2v` | Video takes only (mp4). No SH02 still |
| `App_Data/content-assets`, `content-video`, `content-work`, `os-kf` | Directories do not exist |
| Repo-wide filename `kf-EP01-SC01-SH02-canon.jpg` | **0 files** |
| Size match `151347` B (official SH02 graph hash suffix) | **0 files** under App_Data |
| `pack_content.production_artifacts` | **0 rows** |
| `pack_content.video_keyframe_snapshot` | **0 rows** |
| Official `series_build.graph_json` `runs.EP01-SC01-SH02.keyframeDataUrl` | empty (`data_url_len=0`) |
| Browser IDB `kit-famixa-kf` / `pixels` | **no** key `FAMIXA:kf:4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f:EP01-SC01-SH02` |
| `E:\` / `C:\Users\Admin\Downloads` / `Desktop` for that canon filename | **0 files** |

---

## 2. Official SH02 identity (graph only — not pixels)

| Field | Value | Physical? |
|---|---|---|
| buildId | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | n/a |
| shotId / shotCode | `EP01-SC01-SH02` | n/a |
| filename (graph) | `kf-EP01-SC01-SH02-canon.jpg` | **file missing** |
| storageKey / path | empty (`keyframePath` null) | no |
| mime | unknown (no bytes) | no |
| byte size | unknown; graph hash suffix implies **151347** | no file of that size |
| SHA256 | unknown; graph `kfSourceHash` prefix `h5778a617` | not a file SHA |
| width / height | unknown | no |
| artifactId | none | Production OS empty |
| `kfApproved` | `true` | **flag only** — pixels stripped |

Same pattern on official SH01 / SH03–SH06: filenames `kf-EP01-SC01-SH0N-canon.jpg`, `kfApproved=true`, `keyframeDataUrl` length 0.

---

## 3. Shot scope

SH02 was searched first. Other official-build shots also lack physical KF files.

**Not done:** taking SH01 (or SH05) pixels and treating them as SH02.

Other physical images exist. They are listed below so you can decide. None is an official-build SH02 Keyframe.

---

## 4. Canon / class of every physical image found

### A. Official SH02 Keyframe

**Not found.**

### B. Historical Famixa series KF (other build, other shot)

This browser IndexedDB `kit-famixa-kf` has **one** pixel row:

| Field | Value |
|---|---|
| class | historical Famixa series KF — **not** official SH02 |
| buildId | `fca0c122-797e-4356-9bd1-2a70daac1ed4` (older 11-shot EP01, updated 2026-09-07) |
| shotId / shotCode | `EP01-SC01-SH05` |
| IDB key | `FAMIXA:kf:fca0c122-797e-4356-9bd1-2a70daac1ed4:EP01-SC01-SH05` |
| filename | `kf-EP01-SC01-SH05-canon.jpg` |
| storageKey/path | IndexedDB `kit-famixa-kf` / `pixels` (this Admin origin) |
| mime | `image/jpeg` |
| byte size | **97050** |
| SHA256 | `9cb8c4abef917967531c4bdaf1825ae3fccaaba8d83562b97acd46d7dec0584b` |
| width | 1280 |
| height | 720 |
| artifactId | none |

That graph row on `fca0c122` also has `keyframeDataUrl` stripped. Pixels live only in this session’s IDB. **Do not assign to official SH02.**

No IDB row for official-build SH01–SH06.

### C. Character Canon stills (not shot Keyframes)

IDB `kit-famixa-canon` / `pixels` (this session):

| key | filename | mime | bytes | class |
|---|---|---|---|---|
| `FAMIXA:CHAR-001` | `main-child2.png` | image/png | 251618 | character Canon still |
| `FAMIXA:CHAR-002` | `CHAR-002-nam-master.png` | image/png | 245769 | character Canon still |
| `FAMIXA:CHAR-003` | `Main-mother.png` | image/png | 202225 | character Canon still |

Disk character masters / identity packs (examples):

| path | bytes | SHA256 | w×h | class |
|---|---|---|---|---|
| `App_Data/kit-video-master/CHAR-001/ERA-01/MINH-E01-CANDIDATE-001.jpg` | 70953 | `b825b46053c079b212e44cc040a05d01ffe9f5e981a5f19ee9cf946f4f614224` | 864×1184 | character Canon / master candidate |

Source does **not** bind these to `EP01-SC01-SH02`.

### D. Calibration still

| path | bytes | SHA256 | w×h | class |
|---|---|---|---|---|
| `App_Data/kit-video-master/CALIBRATION/FAMIXA-VISUAL-CALIBRATION-V1/CAL-001_FRONT.jpg` | 163095 | `b696e54a5e3eaa64640e7a53ec96c4564c6ae75d0122261561aaaf13dad7ef3e` | 864×1184 | calibration still |

Not a series shot Keyframe.

### E. Unrelated visual-engine stills

Kit Video engine production `7ce4fe9f-948d-4230-8cfc-a9f5c3e89bf3`, shot code **`SH01-01`** (not Famixa `EP01-SC01-SH02`):

| filename | path | mime | bytes | SHA256 | w×h |
|---|---|---|---|---|---|
| attempt-01.jpg | `App_Data/kit-video-kf/7ce4fe9f948d42308cfca9f5c3e89bf3/SH01-01/attempt-01.jpg` | image/jpeg | 146212 | `50f264190da9d6084ae8164e3bff0efa4d68cd78c0e0db0f982456f0813f605b` | 1280×720 |
| attempt-02.jpg | same folder | image/jpeg | 201513 | `0f686e59f7c316ee81d439d6b13517113a37830652b9fd33c0be4ba82738b5c7` | 1280×720 |
| attempt-03.jpg | same folder | image/jpeg | 142763 | `bb52bad7075edbf48010b9897c6ad6f5901266169f3fb2ba0ea53ca2e140568f` | 1280×720 |

Visual-engine shot `66559efe-3021-42f6-89e2-a4d24f83c7f2`, **`shot_code=CHAR-001-MINH-ERA01-SHOT-001`**:

| filename | path | mime | bytes | SHA256 | w×h |
|---|---|---|---|---|---|
| `7ed003d7…png` | `App_Data/kit-video-kf/production-execution/66559efe302142f689e2a4d24f83c7f2/7ed003d7789a41efbcdfa88637ff14d2.png` | image/png | 2287923 | `4f8b0b8eb936791147a592e2c55a9c5f805673a0090de0b1d652c2e0b888d2b4` | 1344×768 |
| `acc2ed9f…png` | same folder `acc2ed9fa3d8419788a31fb67f35a8ee.png` | image/png | 2436732 | `58ddcc74530ee1f59a20d23cc7d3e3328a51114128e11ab48cd1163037efb017` | 1344×768 |

Unrelated to official Famixa EP01 SH02.

---

## 5. Production OS compatibility (no POST)

`POST /api/content/production/keyframes` requires a real `data:image…;base64,…` whose decoded bytes are **≥ 32**. Controller rejects missing `base64,` or short buffers as `FailedIdentity`.

| Candidate | Enough bytes for that POST? | Valid as official SH02 KF? |
|---|---|---|
| Official SH02 graph filename | no pixels | no |
| IDB `fca0c122` / SH05 97050 B JPEG | yes, technically | **no** — other build, other shot |
| Character Canon / calibration / visual-engine stills | yes, technically | **no** — source is not official SH02 KF |

No official SH02 payload exists to import in a later phase without generating, restoring a missing file from outside this machine, or violating shot identity.

---

## 6. What a later import would need (evidence gap)

For verdict **A** on official SH02 the next phase would need all of:

- buildId `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- shotCode `EP01-SC01-SH02`
- physical JPEG/PNG ≥ 32 B
- path or IDB key scoped to that build+shot
- SHA256 / w×h measured from those bytes

That bundle is **absent** on this workstation.

---

## Paid / writes

0 provider calls. 0 Production OS rows created. 0 files written. 0 imports.
