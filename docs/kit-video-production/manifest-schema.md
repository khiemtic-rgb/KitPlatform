# Schema manifest dự án

File mẫu: `tools/kit-video-production/schemas/project-manifest.schema.json`.

Bản kiểm tra khi chạy là `kit_video.validate`. Schema JSON mô tả hợp đồng. Không cài thêm thư viện jsonschema.

`schemaVersion` là `kit-video-project/1`.

## Nhóm trường

- `projectId`, `projectName`, `brand.id`, `brand.name`. Không có thương hiệu mặc định.
- `script.version`, `script.approvalStatus`: `draft`, `proposed` hoặc `approved`.
- `durationPolicy.mode`: `target` (bắt buộc `targetSeconds` > 0), `voice_driven` (hint có thể null), `explicit_scenes` (không được đặt hint).
- `storyStructure`: một mảng beat. Số beat không cố định.
- `voice`: profile, ngôn ngữ, ghi chú phát âm, mảng track. Track có thể rỗng.
- `assets`: xem [quản lý asset](asset-management.md).
- `visualStyle`: profile, palette (`background`, `ink`, `accent`, `paper`), hai font, `safeArea` bốn cạnh từ 0 đến dưới 0.4.
- `audioProfile`: `ducking`, `voiceGain`, `sfxGain`, `musicPolicy` (`none`, `optional`, `required`).
- `format`: `aspectRatio` dạng `16:9`, `width`, `height`, `frameRate` từ 1 đến 60. Tỷ lệ pixel phải khớp khai báo trong dung sai 2%.
- `render`: H.264 và AAC, `versioned` bắt buộc `true`, `outputRoot`, `allowPlaceholders`.
- `pipeline`: đúng 12 id theo thứ tự trong [kiến trúc](architecture.md).
- `shotPlan`: tên file nằm trong thư mục dự án.

`targetSeconds: 180` trong dự án NOVIXA là hint của dự án đó. Validator cảnh báo khi tổng cảnh lệch hint quá 15% và không sửa tổng.

Thêm trường không làm hỏng file. Thiếu trường bắt buộc thì `validate` in `FAIL` kèm đường dẫn trường.
