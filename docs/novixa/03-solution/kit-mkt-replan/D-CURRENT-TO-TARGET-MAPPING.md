# D. Ánh xạ hiện trạng sang đích

Hiện trạng dưới đây đã đọc từ code và migration. Số liệu trên database thật chưa được đếm. Chỗ đó ghi là việc đối soát, không ghi là lỗi dữ liệu.

## Giữ nguyên

| Thành phần | Căn cứ | Lý do giữ |
|---|---|---|
| `pack_content.brand`, site, channel | `ContentRepository` và `ContentBrandsPage` | Brand đã là hàng trong park |
| `content_series`, `content_episode` | `migrations/383` | Đúng vai trò tổ chức nội dung |
| `topic` và trạng thái biên tập | `migrations/281` | Đúng vai trò Content |
| `publish_job`, `publish_log` | `migrations/282` | Đúng vai trò xuất bản |
| `usage_ledger`, trang budget, trang AI | `migrations/281`, nav thiết lập | Đủ cho chi phí và model |
| `video_universe` thuộc project | `migrations/333` | Đúng vai trò thế giới sản xuất |
| `video_production`, shot state, `series_build_id` | `migrations/333` | Engine và móc Famixa đã tách khỏi series bài |
| `series_build` và graph Famixa | `migrations/376` và các migration production | Workflow `famixa_series`, không phải Series nội dung |
| `video_job`, `video_template` | `migrations/288` | Workflow `factory_short`, đã có `topic_id` |
| Route series, episode, topic, calendar, brands, ai, budget | `router.tsx` | Đường dẫn sâu vẫn đúng |
| Cổng ADMIN của `ContentLayout` | `ContentLayout.tsx` | Chưa có yêu cầu nới quyền |

## Mở rộng

| Thành phần | Từ hiện trạng | Thành |
|---|---|---|
| Brand Context | `useState` riêng trên brand view, topics, series, videos | URL `brand` + context của `ContentLayout` (D1) |
| `content_package` | `brand_id` và `topic_id` đều NOT NULL | Idea mới được NULL cả hai; Angle vẫn đủ cả hai (D2) |
| `CreatePoolAsync` | Chọn brand mã chứa `KIT` hoặc brand active đầu | Không gán brand |
| `content_series.code` | Unique toàn cục | Unique `(brand_id, code)`, fail closed (D4) |
| `content_episode.code` | Unique toàn cục | Unique `(series_id, code)` (D4) |
| `topic` | Không có format | `content_format`, mặc định `article` |
| `video_project` | `brand_code` chuỗi | Thêm `brand_id`, `film_type_id`. Cột cũ chỉ còn là bản sao (D5) |
| `video_production` | Không trỏ Content | `content_topic_id`, `selected_for_publish` (D12) |
| `video_job` mới | Lọc brand cục bộ ở `ContentVideosPage` | `brand_id` lấy từ ngữ cảnh |
| Adapter Famixa | Hằng `FAMIXA`, `FAMILY_A` | Nhận project từ Film Type; hằng chỉ còn cho project seed chưa gắn (D15) |
| Nav | Bảy mục việc + Videos ngoài chuỗi | Năm cửa trong [C](./C-INFORMATION-ARCHITECTURE.md) |
| Calendar | Nhóm mục đăng theo lineage | Nguồn ngày chỉ còn `publish_job` (D11) |

## Tạo mới

Chỉ một bảng: `pack_content.film_type`, mô tả ở [B](./B-DOMAIN-MODEL.md).

Chỉ các route vỏ: `/content/overview`, `/content/work`, `/content/studio`, `/content/studio/projects/:projectId`, `/content/settings/film-types`. Chúng bọc trang đã có, không tạo một bộ trang sản xuất song song.

Không tạo bảng idea, không tạo bảng content output, không tạo service AI hay chi phí thứ hai.

## Không chuyển vào đích

| Thành phần | Quyết định |
|---|---|
| Proof `proof/kit-video-production-v1/outputs/novixa-pain-stock/` | Để ngoài sản phẩm (D14) |
| Tên Film Type ví dụ | Không seed (D8) |
| Gộp `content_series` với `series_build` | Cấm (D3) |
| Dùng `video_universe` làm Film Type | Cấm (D6) |
| Tự đổi `brand_id` của Idea cũ | Cấm cho đến khi có biên bản đối soát (D18) |
| Tự thêm hậu tố vào mã series/episode trùng | Cấm (D4) |

## Dữ liệu cần chuyển

Các bước này là đặc tả cho giai đoạn được phê duyệt sau. Tài liệu này không chạy chúng.

1. **Project Famixa.** Đọc `video_project` nơi `project_code = 'FAMIXA'`. Nếu có đúng một `brand.code` khớp `famixa` không phân biệt hoa thường, ghi `brand_id`. Nếu không có hoặc nhiều hơn một, để NULL và đưa vào danh sách chặn Studio.
2. **Film Type của project đó.** Không tạo trong bước 1. Người vận hành tạo Film Type `workflow_key = famixa_series` trên brand đã khớp, rồi gắn `film_type_id`. Trước khi gắn, `/content/videos` vẫn mở như hôm nay (D17).
3. **Idea cũ.** Liệt kê package không có `source_package_id`, đang đứng trên brand mà `CreatePoolAsync` sẽ chọn, và chưa có package con. Danh sách này chờ quyết định dữ liệu từng hàng. Kiến trúc không tự tách.
4. **Mã series và episode.** Truy vấn nhóm trùng. Có trùng thì dừng migration unique. Không trùng thì đổi index.
5. **Topic.** `UPDATE` format về `article` chỉ như giá trị mặc định của cột mới, không đổi nội dung.
6. **`brand_code`.** Sau khi có `brand_id`, cập nhật chuỗi cho khớp `brand.code`. Hàng chưa có `brand_id` giữ nguyên chuỗi để còn đối soát.

## Đối soát chưa chạy

```sql
-- Trùng mã, kỳ vọng 0 hàng trước khi đổi unique
SELECT code, count(*) FROM pack_content.content_series GROUP BY code HAVING count(*) > 1;
SELECT code, count(*) FROM pack_content.content_episode GROUP BY code HAVING count(*) > 1;

-- Brand khớp project seed
SELECT id, code, name FROM pack_content.brand WHERE lower(code) = 'famixa';

-- Idea không nguồn, để lập biên bản D18
SELECT p.id, p.brand_id, p.title
FROM pack_content.content_package p
WHERE p.source_package_id IS NULL
  AND NOT EXISTS (
    SELECT 1 FROM pack_content.content_package c WHERE c.source_package_id = p.id
  );
```

Kết quả các câu này không đổi D1–D18. Chúng chỉ mở hoặc chặn bước dữ liệu tương ứng trong [E](./E-MIGRATION-ROADMAP.md).
