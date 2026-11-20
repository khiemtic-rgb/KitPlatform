# B. Mô hình miền

Đọc cùng [A-MASTER-ARCHITECTURE.md](./A-MASTER-ARCHITECTURE.md). Không có thực thể nào trong file này được gộp chỉ vì cùng mang tên series, episode, hay project.

## Sơ đồ quan hệ đích

```text
brand
  ├── film_type                         (mới, thuộc brand)
  ├── content_package                   (Idea khi chưa có source; Angle khi có source)
  │     └── content_series              (tổ chức biên tập, không phải graph video)
  │           └── content_episode
  │                 └── topic           (Content; format article | video | infographic | other)
  │                       └── publish_job
  └── video_project                     (một brand, một film_type)
        └── video_universe              (thế giới của project)
              └── video_production      (workflow famixa_series hoặc guided_production)
                    └── video_shot_state

video_job                               (workflow factory_short; đã có brand, package, topic)
```

`series_build` tiếp tục sống trong graph Famixa. `video_production.series_build_id` là móc tuỳ chọn khi `workflow_key = famixa_series`. Không có khóa từ `content_series` sang `series_build`.

## Brand

| Mục | Quy tắc |
|---|---|
| Vai trò | Ngữ cảnh làm việc và chủ của Angle, Series, Content, Film Type, kênh đăng |
| Khóa | `pack_content.brand.id` |
| Giữ | Bảng brand, site, channel, trang `/content/brands` |
| Sửa | Không đổi cấu trúc brand trong roadmap này |
| Rủi ro | Brand là hàng trong park, không phải tenant. Không được tạo tenant mới để tách Film Type |

## Idea và Brand Angle — `content_package`

Hôm nay một bảng đóng cả hai vai (`migrations/287_pack_content_package.sql`):

- Idea: không có `source_package_id`. Pool trong `ContentIdeaPoolPage.tsx` gọi đây là Core Idea.
- Angle: có `source_package_id` trỏ về Idea.
- Cả hai đang buộc `brand_id` và `topic_id`. `CreatePoolAsync` chọn brand có mã chứa `KIT`, không có thì brand active đầu tiên, rồi tạo package trên brand đó.

Đích (D2, D18):

| Vai | brand_id | topic_id | source_package_id |
|---|---|---|---|
| Idea mới | NULL | NULL | NULL |
| Brand Angle | NOT NULL | NOT NULL | Idea |
| Package thường đã có | giữ nguyên | giữ nguyên | giữ nguyên |

Ràng buộc cần thêm khi được phép viết migration:

- Idea không brand thì không có topic.
- Package có brand thì phải có topic.
- Angle phải trỏ tới một Idea, và brand của Angle là brand của góc đó, không sao chép từ brand của Idea vì Idea không có brand.

Tạo và chuyển trạng thái:

- Tạo Idea chỉ cần tiêu đề. Không đọc Brand Context.
- Chấm brand fit không tạo Angle. Việc áp fit mới tạo Angle, đúng hành vi pool hiện tại.
- Một Idea có nhiều Angle, mỗi Angle một brand.
- Xoá Idea bị chặn khi còn Angle, vì `source_package_id` của series và package đang `ON DELETE RESTRICT` / `SET NULL` tuỳ cột. Giữ `RESTRICT` từ series sang package. Không đổi hành vi xoá của Angle trong giai đoạn này.
- Hàng Idea cũ không bị đặt `brand_id = NULL` cho đến khi có danh sách đối soát (D18).

Giữ bảng `content_package`. Không tạo bảng `idea`.

## Series nội dung — `content_series`

| Mục | Quy tắc |
|---|---|
| Vai trò | Nhóm Content có liên hệ biên tập trong một brand |
| Khóa | `id`. Đích: unique `(brand_id, code)` thay cho `uq_content_series_code` toàn cục |
| Brand | `brand_id` bắt buộc, đã có |
| Film Type | Không có. Series không biết mình sẽ được quay kiểu nào |
| Tạo | Từ một Brand Angle (`source_package_id`). Không tạo từ Idea chưa có brand |
| Trạng thái | Giữ check hiện tại: `DRAFT`, `PLANNED`, `ACTIVE`, `PAUSED`, `COMPLETED`, `CANCELLED` |
| Giữ | Bảng, route `/content/article-series`, lineage |
| Đổi | Unique mã theo brand, sau khi truy vấn trùng trả về rỗng hoặc đã được sửa tay (D4) |

`episode_count` vẫn là kế hoạch số tập. Không dùng cột này cho Film Type.

## Episode nội dung — `content_episode`

| Mục | Quy tắc |
|---|---|
| Vai trò | Phần trong một Series, có thể chưa có Content |
| Khóa | `id`. Giữ unique `(series_id, episode_no)`. Đích: unique `(series_id, code)` thay cho unique `code` toàn cục |
| Brand | Qua series, không thêm `brand_id` riêng |
| Film Type | Không có |
| Tạo | Trong series. `content_topic_id` để trống cho đến khi tạo Content cho tập đó. Unique một topic cho một episode được giữ |
| Trạng thái | Giữ check từ `PLANNED` đến `CANCELLED` trong `migrations/383` |
| `planned_at` | Mốc kế hoạch biên tập. Không phải `publish_at` và không phải trạng thái shot |

Không nối episode nội dung với shot của `video_production`.

## Content — `topic`

| Mục | Quy tắc |
|---|---|
| Vai trò | Đơn vị nội dung được biên tập và, khi cần, được xuất bản |
| Khóa | `id`. `brand_id` vẫn `NOT NULL` |
| Format | Cột mới `content_format`, mặc định `article` cho hàng cũ. Giá trị: `article`, `video`, `infographic`, `other` |
| Series | Tuỳ chọn, qua `content_episode.content_topic_id` |
| Tạo | Trong một brand. Có thể đi từ episode, từ Angle, hoặc tạo độc lập. Không tạo topic cho Idea chưa có brand |
| Trạng thái | Giữ `Draft`, `Generating`, `Review`, `Approved`, `Scheduled`, `Published`, `BudgetBlocked`, `Rejected` |
| Video | Format `video` cho phép mở Studio. Nó không tự tạo project |

`Scheduled` và `Published` trên topic chỉ được đặt từ kết quả `publish_job`, không từ `video_production.state = FINAL`.

## Film Type — bảng mới `pack_content.film_type`

| Cột | Quy tắc |
|---|---|
| `id` | Khóa chính |
| `brand_id` | NOT NULL, khóa ngoại tới `brand`, xoá brand thì xoá Film Type |
| `code` | Unique `(brand_id, code)` |
| `name` | Tên do người vận hành đặt |
| `workflow_key` | `famixa_series`, `factory_short`, hoặc `guided_production` |
| `status` | `draft`, `active`, `archived` |
| `config_json` | Tỷ lệ khung, hồ sơ bước, ràng buộc của nhánh. Không chứa secret |

Không có hàng seed. Tên trong yêu cầu nghiệp vụ (phim giáo dục, phim quảng cáo, phim giới thiệu) chỉ minh hoạ cardinality, không được ghi thành dữ liệu (D8).

Một brand có nhiều hàng. Một hàng không thuộc hai brand. Đổi `workflow_key` bị chặn khi đã có project ở trạng thái khác `draft` và `archived`, vì đổi khóa là đổi loại graph thực thi.

## Production Project — `video_project`

Hiện có `project_code`, `name`, `brand_code` chuỗi, `visual_json`, `rules_json` (`migrations/333`). Project seed `FAMIXA` dùng `brand_code = 'famixa'`.

Đích (D5, D9):

| Cột | Quy tắc |
|---|---|
| `brand_id` | Khóa ngoại tới `brand`, thêm mới, nullable trong lúc đối soát rồi thành bắt buộc với mọi project muốn mở Studio |
| `film_type_id` | Khóa ngoại tới `film_type`. Film Type phải cùng `brand_id` với project. Trigger hoặc kiểm tra ở application layer, vì khóa ngoại đơn không chứng minh cùng brand |
| `brand_code` | Giữ cột. Khi có `brand_id`, giá trị bị ghi đè bằng `brand.code`. Cấm dùng cột này trong `WHERE` của màn mới |

Tạo project chỉ khi ngữ cảnh đã có brand và một Film Type `active` của brand đó. Project không kéo theo `publish_job`.

Tài nguyên của project được truy vấn theo `project_id`. API Studio từ chối nếu `project.brand_id` hoặc `project.film_type_id` khác ngữ cảnh.

Hàng `FAMIXA` chưa khớp brand nào thì để `brand_id` NULL. Không tạo brand `famixa` trong migration.

## Universe — `video_universe`

Giữ nguyên vai: thế giới của một project, unique `(project_id, universe_code)`.

Không thêm `brand_id` hay `film_type_id`. Brand và Film Type được suy ra từ project. Nhờ đó universe không thể được dùng như một kiểu phim độc lập.

`FAMILY_A` tiếp tục là universe của project Famixa đã seed. Adapter không được coi mã này là Film Type (D6, D15).

## Production — `video_production` và `video_job`

Hai bảng thực thi cùng tồn tại vì chúng đã có vòng đời khác nhau.

`video_production` (`migrations/333`):

- Thuộc project và universe.
- `state` từ `DRAFT` đến `FINAL` giữ nguyên.
- `series_build_id` chỉ được ghi khi Film Type của project là `famixa_series`.
- Thêm `content_topic_id` NULL, khóa ngoại tới `topic`, `ON DELETE SET NULL`.
- Thêm `selected_for_publish boolean NOT NULL DEFAULT false`.
- Unique một phần: tối đa một hàng `selected_for_publish` cho cùng `content_topic_id`.
- Phim độc lập để `content_topic_id` NULL. Gắn topic sau được, với điều kiện topic cùng brand với project và `content_format = video`.

`video_job` (`migrations/288`):

- Giữ `brand_id`, `package_id`, `topic_id`, template, storyboard, trạng thái factory.
- Là thực thi của `factory_short`.
- Không sao chép sang `video_production`.
- Job mới phải lấy `brand_id` từ Brand Context, không từ bộ lọc cục bộ của trang.

Shot state thuộc production. Không thêm brand lên từng shot.

## Đầu ra và xuất bản

| Việc | Quan hệ |
|---|---|
| Video làm cho một Content, lối factory | `video_job.topic_id` |
| Video làm cho một Content, lối engine | `video_production.content_topic_id` |
| Chọn bản để phát hành | `selected_for_publish` trên production; với factory, job trạng thái `Approved` mới được topic dùng |
| Lên lịch | `publish_job.topic_id` và `publish_job.brand_id` |
| Xem lịch | Chỉ `publish_job` |

Không có bảng `content_output`. Một cột trên bảng thực thi là đủ (D12).

## Publishing Calendar

Không có bảng calendar riêng. Lịch là hình chiếu của `publish_job`.

Nhóm theo brand, rồi theo series và episode khi topic có lineage, là cách hiển thị. Nó không biến episode thành một lịch sản xuất.

## Ma trận tạo và cấm

| Từ | Được tạo | Không được tạo |
|---|---|---|
| Idea | Angle khi người dùng áp fit | Topic, series, project, publish job |
| Angle | Series, hoặc Content trực tiếp | Project của brand khác |
| Series | Episode trong cùng series | Film Type |
| Episode | Một Content | Shot |
| Content video | Project hoặc video job cùng brand | publish job khi chưa Approved |
| Film Type | Project của đúng brand | Universe đứng một mình |
| Project | Universe, production | publish job |
| Production FINAL | Gắn topic nếu chưa có, rồi chờ duyệt nội dung | Tự ghi `publish_at` |
