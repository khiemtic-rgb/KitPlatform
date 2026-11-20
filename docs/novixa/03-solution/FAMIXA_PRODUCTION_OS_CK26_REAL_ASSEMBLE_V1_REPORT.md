# CK26 — REAL ASSEMBLE V1

Date: 2026-09-24  
Mode: **ONE official SH02 remux**. No provider. No episode concat. No SH01 mutation.

## 1. VERDICT

**PASS**

## 2. TARGET

- Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- Shot: `EP01-SC01-SH02`

## 3. INPUT AUTHORITY

- MIX `58297402-e252-45e8-8d33-c7414749a297`
- SHA `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`
- Role `MIX` VERIFIED 8.000s
- `ACCEPTED:MIX` = `CURRENT:MIX` = `VISIBLE:MIX` = generic `ACCEPTED` = same id

## 4. FREEZE

- Snapshot ID: `a108efd6-374e-46ea-ab6e-bc93afe59c57`
- Snapshot Hash: `281f5fb6125d94af0d27e3fd53759c25633038a58495283b02201712b89cf7bd`
- Token: `1c210b73333e46ce9d028beab8ef2f5d|4ef164d610aa42fdb3d2acff9b3d2c7f|0|EP01-SC01-SH02|MIX|58297402e25245e88d33c7414749a297|e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b|os/ff28d642765942839894f89bfbeb897c|8`

## 5. EXECUTION

- Request: `79385101-e4f6-4080-809e-867096460a4b`
- Task: `c53fb9ef-a152-4795-9610-81a8cd3c294f`
- Attempt: `c82d57f5-6827-4b78-a5af-1ba10385715c`
- Command: `ffmpeg -y -hide_banner -loglevel error -i input.mp4 -map 0:v:0 -map 0:a:0 -c copy -movflags +faststart output.mp4`
- Remux 1, compose 0

## 6. OUTPUT ARTIFACT

- `92621f9e-57eb-4185-8cea-a962ec399136`
- SHA `37127ee5bf4edd8ee1b01f60c4686a9c6ff0dec132b684044fe5c109c65629bb`
- Size `1423715` MIME `video/mp4` duration `8` storage `os/c82d57f568274b78a5af1ba10385715c`
- ffprobe format/video `8.000000` / 192 frames

## 7. PROVENANCE

- Parent MIX `58297402-…` SHA `e73c85db…`
- No VIDEO / LIPSYNC / VOICE parent

## 8. BINDINGS

- `CURRENT:ASSEMBLY` = `92621f9e-…`
- No `ACCEPTED:ASSEMBLY`
- `ACCEPTED:VIDEO_TAKE` / `VOICE` / `LIPSYNC` / `MIX` unchanged

## 9. FILM ASSEMBLY

- New `1c210b73-333e-46ce-9d02-8beab8ef2f5d` shot `21678c2e-162b-4f6b-a006-5c3ad3faae36`
- SH02 membership MIX `58297402-…` duration 8
- SH01 `d56a3c63-…` / `2f88e0a0-…` / 5.042 untouched

## 10. REPLAY

- First remux 1. Replay same artifact. Final remux 1.

## 11–15

Isolated A–J PASS. CK20/22/23B/24D/SH01 PASS. Providers 0. Next: episode-level FilmAssembly concat / FINAL — not implemented here.

HARD STOP.
