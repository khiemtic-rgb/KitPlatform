# Quy tắc quản lý asset

Mỗi asset có `assetId`, `kind`, `source`, `path`, `pathBase`, `version`, `usageRights`, `approval`, `role`, `label`.

`source` là một trong: `user_supplied`, `library`, `generated`, `placeholder`.

`approval` là một trong: `missing`, `draft`, `placeholder`, `proposed`, `approved`, `rejected`.

`pathBase` là `project`, `repo` hoặc `absolute`. Đường dẫn `repo` tính từ `--repo`.

## Luật chặn

- `missing` hoặc `rejected`: lỗi lúc validate nếu cảnh có tham chiếu, và BLOCKED lúc render.
- `placeholder` không được `approved`.
- `role: product_ui` chỉ được `source` là `user_supplied` hoặc `library`, và `approval: approved`. Placeholder không được đóng vai UI.
- UI có `demoData: true` phải có `demoLabel` đúng chuỗi `Dữ liệu minh họa`. Template `ui_showcase` vẽ nhãn đó suốt cảnh và QA kiểm nhãn có trong `layout.json`.
- `allowPlaceholders: false` thì không render asset `placeholder`.
- File khai trong `path` mà không có trên đĩa: BLOCKED. Không thay bằng hình khác.
- Logo thư viện chỉ được trỏ tới file gốc. Style proof không sửa `docs/novixa/assets/brand/novixa-logo.png`.

## Bốn loại trong pilot

| Loại | Ví dụ | Trạng thái |
| --- | --- | --- |
| Thư viện | Logo NOVIXA, ba ảnh UI trong DOC-008 | Logo `approved` để dùng nguyên file. Ba ảnh UI đang `proposed` và `role: reference`, chưa được dựng thành sản phẩm |
| Người dùng | Chưa có file mới trong lần này | — |
| Code vẽ | Hộp, két, lịch, người hình học | `proposed`. Không phải thiết kế cuối |
| Placeholder | `whoosh`, `thud`, `paper`, `lock` trong `proof/kit-video-production-v1/library/sfx` | `placeholder`. Mỗi file có `.label.txt`. Render log liệt kê chúng |

Ảnh UI `01` đến `10` trong DOC-008 không bị sửa. Chỉ ba ảnh liên quan lời thoại được ghi vào manifest phim, và chưa được đưa vào style proof.
