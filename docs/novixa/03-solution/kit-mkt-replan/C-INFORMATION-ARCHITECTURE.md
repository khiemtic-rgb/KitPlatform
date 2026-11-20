# C. Kiến trúc thông tin

Đối chiếu với menu hiện tại trong `client/admin/src/modules/content/content-nav.tsx` và route trong `client/admin/src/app/router.tsx` khoảng dòng 1121–1168. Shell đọc các mảng nav này qua `client/admin/src/shared/components/AppLayout.tsx`.

## Menu hiện tại

Nhóm làm việc, mỗi mục là một hàng sidebar:

- Hôm nay → `/content/ops`
- Ý tưởng → `/content/pool`
- Thương hiệu → `/content/brand-view`
- Góc brand → `/content/packages`
- Series → `/content/article-series`
- Nội dung → `/content/topics`
- Lịch → `/content/calendar`

Nhóm thiết lập: Cấu hình thương hiệu `/content/brands`, Chi phí AI `/content/budget`, Model AI `/content/ai`, Tuỳ chọn `/content/settings`.

Videos `/content/videos` nằm trong `CONTENT_NAV_OUTSIDE`. Comment trong `content-nav.tsx` nói Videos ở ngoài chuỗi Idea → Episode → Content. Smoke `content-article-series.smoke.ts` đang khẳng định điều đó. Khi đổi nav, smoke này phải đổi theo, ở giai đoạn triển khai chứ không phải trong tài liệu này.

`ContentLayout` không vẽ menu và không giữ brand.

## Menu đích

Năm cửa. Brand không phải một cửa; nó là thanh ngữ cảnh trên cùng của khu Content.

| Cửa | Route | Bên trong |
|---|---|---|
| Tổng quan | `/content/overview` | Việc trong ngày của brand đang chọn, hoặc bản tổng hợp khi `brand=all` |
| Content | `/content/work` | Rail: Ý tưởng, Góc brand, Series, Nội dung. Episode là trang con của Series |
| Video Studio | `/content/studio` | Cổng Brand + Film Type, rồi project và workflow |
| Publishing | `/content/calendar` | Lịch và trạng thái `publish_job` |
| Cấu hình | `/content/settings` | Thương hiệu, Film Type của brand, AI, chi phí |

Idea, Brand Angle, Series, Episode và Content không còn là mục sidebar cùng cấp với Studio.

Thanh ngữ cảnh:

- Chọn một brand, hoặc Tất cả brand.
- Trong Studio, chọn thêm Film Type thuộc brand đó.
- Đủ ngữ cảnh rồi thì không hỏi lại (D9).
- Thiếu brand hoặc Film Type thì Studio dừng ở cổng chọn. Cổng không có nút “dùng brand đầu tiên”.

## Route đích và alias

| Route cũ | Xử lý |
|---|---|
| `/content`, `/content/` | Vẫn vào Tổng quan. `ContentLayout` hôm nay đẩy sang `/content/ops`; đích đẩy sang `/content/overview` |
| `/content/ops` | Giữ, render Tổng quan |
| `/content/pool` | Giữ. Không lọc theo brand đang chọn |
| `/content/packages` | Giữ. Bắt buộc một brand, không nhận `all` để tạo mới |
| `/content/article-series` và `.../episodes/:episodeId` | Giữ nguyên đường dẫn |
| `/content/topics`, `/content/topics/:id` | Giữ. Workspace của một topic đọc brand của topic, không đọc bộ lọc cũ của trang danh sách |
| `/content/brand-view` | Giữ. Là trang hồ sơ brand, mở từ thanh ngữ cảnh, không còn là mục sidebar riêng |
| `/content/brands` | Giữ. Là cấu hình brand, mở từ Cấu hình |
| `/content/calendar` | Giữ. Là cửa Publishing |
| `/content/budget`, `/content/ai` | Giữ đường dẫn, gom dưới Cấu hình |
| `/content/videos` | Giữ nguyên component hiện tại cho đến khi project Famixa có `brand_id` và `film_type_id`. Sau mốc đó chuyển sang `/content/studio?brand=&film=` (D17) |
| `/content/visual-calibration/:packId` | Giữ. Chỉ mở khi workflow của Film Type là `famixa_series` |

Route mới:

- `/content/overview`
- `/content/work` chuyển tiếp tới rail mặc định là Nội dung khi đã có brand, và tới Ý tưởng khi chưa có brand
- `/content/studio`
- `/content/studio/projects/:projectId`
- `/content/settings/film-types` cấu hình Film Type của brand trên URL

Query dùng chung:

- `brand=<uuid>` hoặc `brand=all`
- `film=<uuid>` chỉ trên Studio và các route production của project

Đổi `brand` xoá `film` nếu Film Type không thuộc brand mới.

## Luồng điều hướng

1. Vào Content. Nếu URL chưa có brand, khôi phục brand xem gần nhất trong session trình duyệt. Không có thì mở Tổng quan ở chế độ chưa chọn, với lối sang Idea và lối chọn brand.
2. Chọn brand một lần. Packages, Series, Topics, Calendar và cổng Studio đọc cùng giá trị.
3. Trong Content, rail đi Idea → Góc → Series → Nội dung. Người dùng được nhảy tới Nội dung để tạo bản độc lập.
4. Từ một Content `video`, nút sản xuất mở Studio với `brand` của topic và yêu cầu chọn Film Type nếu URL chưa có `film` hợp lệ.
5. Từ Studio, người dùng có thể tạo phim chưa gắn Content. Màn đó không chuyển sang Calendar.
6. Khi Content `video` đã `Approved` và có đầu ra được chọn, Publishing mới tạo được `publish_job`.

`brand=all` cho phép đối soát danh sách và lịch. Nút tạo Angle, Series, Content, project và lịch đăng ở trạng thái không bấm được, kèm lý do phải chọn một brand.

## Trang hiện tại được đặt vào đâu

| Trang | Chỗ đích |
|---|---|
| `ContentOpsPage` | Tổng quan |
| `ContentIdeaPoolPage` | Rail Content, ngoài brand |
| `ContentPackagesPage` | Rail Content, trong brand |
| `ContentArticleSeriesPage`, detail, episode | Rail Content, trong brand |
| `ContentTopicsPage`, `ContentTopicWorkspacePage` | Rail Content, trong brand |
| `ContentBrandViewPage` | Hồ sơ mở từ thanh brand |
| `ContentVideosPage` ba tab | Tách theo workflow, không còn là một trang ba tab độc lập với brand |
| Tab Series Famixa | Workflow `famixa_series` của Film Type được gắn project Famixa |
| Tab Phase 1 | Bảng của cùng workflow Famixa, không phải một sản phẩm thứ hai |
| Tab Factory | Workflow `factory_short` |
| `ContentCalendarPage` | Publishing |
| `ContentBrandsPage`, AI, budget, settings | Cấu hình |

Tab Famixa không được hiện khi Film Type đang chọn có `workflow_key` khác `famixa_series`. Factory không được hiện khi khóa là `famixa_series`. Nhờ vậy nhân vật và prompt của Famixa không nằm trên màn của một nhánh Novixa.

## Ngữ cảnh nhánh phim

- Film Type được chọn sau Brand và được ghi trên URL.
- Mọi lời gọi danh sách project, universe, production, shot và asset gửi kèm `project_id` đã kiểm tra cùng brand và cùng Film Type.
- Đổi Film Type không giữ project đang mở.
- Không có Film Type “chung” cho mọi brand.
- Cấu hình Film Type nằm ở Cấu hình, không nằm trong màn đang dựng phim, để người đang sản xuất không sửa quy trình của nhánh khác.
