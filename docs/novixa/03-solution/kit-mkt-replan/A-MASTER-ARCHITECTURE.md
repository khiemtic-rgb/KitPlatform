# A. Kiến trúc đích

Đọc cùng [00-INDEX.md](./00-INDEX.md). Mọi quyết định trong file này dùng mã D1–D18.

## 1. Park và ranh giới

KIT_MKT là một tenant marketing. Thương hiệu nội dung là hàng `pack_content.brand`, không phải tenant ERP. Điều này đã được ghi trong `docs/novixa/03-solution/kit-content-park-v1.md`.

| Lớp | Việc được làm | Việc không được làm |
|---|---|---|
| Brand Workspace | Chọn brand, xem tổng hợp, khóa ngữ cảnh cho các khu phía dưới | Không lưu lựa chọn brand riêng trên từng trang |
| Content | Idea, Brand Angle, Series, Episode, Content | Không chứa tiến độ shot, nhân vật, hay lịch đăng |
| Video Studio | Film Type, project, quy trình sản xuất, đầu ra video | Không gộp với `content_series`; không tự chọn brand |
| Publishing | Duyệt phát hành, lịch, kết quả đăng | Không nhận trạng thái sản xuất làm ngày đăng |
| Shared services | AI, chi phí, credential, site, channel, quyền ADMIN | Không tách process hay schema mới |

`ContentLayout.tsx` hiện chỉ kiểm tra ADMIN và module `kit_content`, rồi đẩy `/content` sang `/content/ops`. Layout này trở thành nơi giữ Brand Context. Nó không trở thành một ứng dụng riêng.

Migration mới, khi được phê duyệt để viết ở giai đoạn sau, chỉ vào `deploy/ubuntu/migration-files.content.txt`.

## 2. Bốn luồng

```text
Ngữ cảnh
  chọn Brand  →  các khu Content, Studio, Publishing cùng đọc ngữ cảnh đó
  chế độ Tất cả brand  →  chỉ để xem và đối soát, không tạo bản ghi gắn brand

Nội dung
  Idea (chưa có brand)
    → Brand Angle (một brand)
      → Series (tuỳ chọn)
        → Episode (tuỳ chọn)
          → Content
  Content cũng được tạo thẳng trong một brand, không qua chuỗi

Sản xuất
  Brand → Film Type → Production Project → Workflow
  Workflow đọc Content có sẵn, hoặc mở một phim chưa có Content

Xuất bản
  Content đã duyệt → publish_job → Calendar
  Project và shot không có publish_at
```

Ba đồng hồ không ghi đè nhau (D10):

| Đồng hồ | Nơi giữ | Ví dụ trạng thái đã có |
|---|---|---|
| Sản xuất | `video_production.state`, `video_job.status`, shot state | `DRAFT` … `FINAL`; `Draft` … `Approved` |
| Biên tập | `topic.status` | `Draft`, `Review`, `Approved`, `Rejected` |
| Xuất bản | `publish_job.status` và `publish_at` | `Queued`, `Running`, `Succeeded`, `Failed`, `Cancelled` |

`Approved` của một video job là xong bản render. `Approved` của topic là nội dung được phép đưa đi xuất bản. Hai giá trị này không đồng nghĩa và không được đồng bộ tự động.

## 3. Brand Context

Hiện mỗi màn một state: `ContentBrandViewPage` có `selectedBrandId`, `ContentTopicsPage` và `ContentVideosPage` có `brandFilter`, `ContentArticleSeriesPage` có `brandId`. Đổi brand ở một màn không đổi màn khác.

Đích (D1):

- URL là nguồn để chia sẻ và mở lại: `?brand=<uuid>` hoặc `?brand=all`.
- `ContentLayout` đọc URL và đưa ngữ cảnh xuống các trang. Trang không còn `useState` brand của riêng mình.
- Lần vào sau, nếu URL không có `brand`, được khôi phục brand đã xem gần nhất từ session của trình duyệt. Việc khôi phục này không được ghi vào `brand_id` của bất kỳ hàng mới nào.
- `brand=all` mở được Tổng quan, danh sách Content, Series, Publishing. Mọi thao tác tạo Angle, Series, Episode, Content, lịch đăng và project đều bị chặn cho đến khi chọn một brand.
- Idea là ngoại lệ có chủ đích (D2). Pool hiện khi chưa chọn brand và khi đang ở `all`. Tạo Idea không đọc brand đang chọn.
- Đổi brand thì Film Type đang mở bị bỏ nếu Film Type đó không thuộc brand mới. Không giữ film của brand cũ trong ngữ cảnh brand mới.

## 4. Content

Chuỗi định hướng là Idea → Brand Angle → Series → Episode → Content. Không phải cổng bắt buộc.

- Idea là `content_package` chưa có `source_package_id`. Đích: `brand_id` và `topic_id` đều trống. Hôm nay `CreatePoolAsync` chọn một brand nhà vì `content_package.brand_id` và `topic.brand_id` đều `NOT NULL` (`migrations/287`, `migrations/281`).
- Brand Angle là package con, có `source_package_id`, và là điểm đầu tiên mang brand. Series bắt buộc `source_package_id` (`migrations/383`).
- Series và Episode diễn tả quan hệ biên tập. Một Content không thuộc series vẫn hợp lệ. UI đã có `ContentKindTag` với `series` và `standalone` trong `content-lineage.tsx`.
- Content là `topic`. Đích thêm `content_format`: `article`, `video`, `infographic`, `other`. Hàng cũ mặc định `article`.
- Định dạng nằm trên Content. Cách làm ra video nằm ở Studio.

`content_series` không được nối với `series_build` bằng cách đổi tên hay gộp bảng (D3). Comment trong `migrations/383` đã tách hai hệ này. `video_production.series_build_id` tiếp tục là móc nối tuỳ chọn của workflow Famixa, không phải khóa của Series nội dung.

## 5. Video Studio

Mô hình là Brand → Film Type → Project → Workflow (D6, D7, D9).

- Film Type thuộc một brand, mã unique trong brand đó. Một brand có nhiều Film Type.
- Project thuộc đúng một brand và đúng một Film Type. Tài nguyên, nhân vật, prompt và universe của project không được đọc từ project khác.
- `video_universe` vẫn thuộc `video_project`, như `migrations/333` đang định nghĩa. Nó không được đưa lên menu như một kiểu phim.
- `brand_code` trên project không còn là quan hệ. Project seed `FAMIXA` có `brand_code = 'famixa'`; đó là chuỗi, không phải khóa ngoại.
- Workflow được chọn bởi `workflow_key`, không phải bằng một engine riêng:
  - `famixa_series`: graph Famixa hiện có, gồm `series_build` và adapter trong `kit-video-famixa-adapter.ts`.
  - `factory_short`: `video_job` của `migrations/288`, đã có `brand_id`, `package_id`, `topic_id`.
  - `guided_production`: `video_production` của engine chung, không đi qua graph Famixa.
- Studio nhận Content có `content_format = video`, hoặc tạo phim độc lập với `content_topic_id` trống (D13).
- Khi sản xuất cho một Content, `video_job.topic_id` hoặc `video_production.content_topic_id` phải trỏ tới topic đó (D12).
- Nhiều lần làm lại được giữ. Trong các bản `FINAL` của cùng một topic, chỉ một bản được đánh dấu dùng để xuất bản.

Adapter Famixa hôm nay khóa `FAMIXA_PROJECT` và `FAMIXA_UNIVERSE`. Đích: màn Studio truyền project và universe của Film Type đang mở. Hai hằng số chỉ còn dùng khi mở đúng project seed chưa gắn Film Type, và bị xoá khỏi đường chính sau D17.

Bản Novixa trong thư mục proof không có route, không có project, không có topic (D14). Không copy file render vào `pack_content` trong roadmap này.

## 6. Publishing

`publish_job` đã thuộc topic và brand (`migrations/282`). Calendar nhóm các mục này theo brand và, khi topic có lineage, theo series và episode (`ContentCalendarPage.tsx`).

Đích (D11, D13):

- Nguồn ngày của Calendar là `publish_job.publish_at`.
- `content_episode.planned_at` là kế hoạch biên tập. Nếu còn được vẽ, phải mang nhãn kế hoạch nội dung và không được tính là lịch đăng hay lịch sản xuất.
- `video_production.state` và shot state không xuất hiện trên Calendar.
- Tạo project không tạo `publish_job`.
- Phim chưa có topic không có hàng trên Calendar.

## 7. Shared services

Các thứ dưới đây đã có chủ và tiếp tục phục vụ cả Content lẫn Studio. Không nhân bản theo Film Type.

| Nhu cầu | Chỗ đang có |
|---|---|
| Trần chi phí | `pack_content.usage_ledger`, route `/content/budget` |
| Model và credential | `/content/ai`, credential production hiện có |
| Kênh và site của brand | `site_target`, `channel_target`, cấu hình trong `ContentBrandsPage` |
| Nhật ký phát hành | `publish_log` |
| Quyền vào khu Content | `ContentLayout` yêu cầu ADMIN |

Film Type chỉ được giữ cấu hình quy trình trong `config_json` của chính nó (tỷ lệ khung, hồ sơ workflow, ràng buộc prompt). Khóa API và trần tiền không chuyển vào Film Type.

## 8. Điều không làm

- Không biến mỗi đối tượng Idea, Angle, Series, Episode, Content thành một mục menu ngang hàng với Studio.
- Không tạo tenant mới cho từng brand hay từng Film Type.
- Không đưa `video_universe` lên vai trò Film Type.
- Không nhập proof Novixa để “có dữ liệu mẫu”.
- Không đổi unique mã series/episode bằng cách tự thêm hậu tố.
