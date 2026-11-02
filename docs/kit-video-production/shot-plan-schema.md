# Schema shot plan

File mẫu: `tools/kit-video-production/schemas/shot-plan.schema.json`. `schemaVersion` là `kit-video-shot-plan/1`. `projectId` phải trùng manifest.

`approvalStatus` của cả plan là `draft`, `proposed` hoặc `approved`. Mỗi cảnh có trạng thái riêng: `proposed`, `approved`, `style_proof`.

## Mỗi cảnh

- `sceneId` duy nhất.
- `storyGoal`, `dialogue`, `action`. `dialogue` được phép rỗng nếu cảnh cố ý không có lời.
- `durationSeconds` > 0. `durationSource` là `fixed` hoặc `voice_track`.
- Với `voice_track`, thời lượng lúc dựng là `voiceLeadSeconds + độ dài file + voiceTailSeconds`. Số trong file là ước lượng cho đến lúc đo.
- `visualMetaphor`: chuỗi hoặc `null`.
- `onScreenText`: các chuỗi phải xuất hiện trên hình. QA so với `layout.json`.
- `templateId` và `params`. Template chưa có trong catalog thì render BLOCKED, kể cả id `custom`.
- `assets`: id đã khai trong manifest.
- `audio.voiceTrackId`, `audio.sfx[]` (`assetId`, `atSeconds`), `audio.intentionalSilences[]` (có `reason`), `audio.syncNotes`.
- `transitionOut`: object hoặc `null` ở cảnh cuối.
- `acceptance`: ít nhất một tiêu chí cho người duyệt.
- `previewFractions`: tùy chọn, các mốc 0–1 để lưu khung xem.

## Phân tích kịch bản

`analyze` đọc markdown có các khối `## segment <id>` và các trường `goal`, `template_hint`, `duration_hint_seconds`, `dialogue`, `on_screen`, `acceptance`, `metaphor`, `visual`, `transition`.

Kết quả luôn `proposed`. Thời lượng đề xuất bằng số ký tự chia `--chars-per-second` (mặc định 13) cộng đầu 0,4 giây và đuôi 0,9 giây. Nếu có hint và ước lượng dài hơn hint 0,25 giây, có cảnh báo `voice_exceeds_hint`. Ước lượng không bị cắt xuống hint.

Sửa shot plan bằng tay trước khi đổi `approvalStatus` của cảnh thành `approved`.
