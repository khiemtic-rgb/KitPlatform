# Thư viện hình cho KIT Video

Catalog nằm ở `catalog.json`. Mỗi dòng là một tài nguyên. Bộ dựng chỉ dùng dòng `approval: approved`, không có `parentId`, và không đánh dấu `derived`.

## Thêm một ảnh

1. Đặt file vào repo. Không sửa byte của logo đã duyệt.
2. Thêm một dòng vào `SCREENS` hoặc `LOGOS` trong `build_v2_films.py`, hoặc thêm thẳng vào `catalog.json` nếu không chạy lại script.
3. Điền đủ: `assetId`, `brand` (`NOVIXA`, `AgentTrading` hoặc `Shared`), `kind`, `category`, `keywords`, `style`, kích thước, `source`, `license`, `approval`, `path`, `sha256`, `averageHash`, `similarityGroup`, `parentId`, `usage`, `allow`, `limits`.
4. Chạy lại script catalog. Ảnh trùng byte hoặc khác hash nhìn dưới 4 bit được gắn `parentId` và không tính là ảnh mới.
5. Ảnh chưa rõ giấy phép để `approval: review`. Ảnh không đạt để `rejected`. Video chính chỉ nhận `approved`.

Crop, đổi màu hoặc đổi tên không được khai thành asset mới. Ghi `parentId` trỏ về ảnh gốc.

Ảnh tham chiếu Wikimedia nằm trong `PHOTOS` của `build_v2_films.py` và file `stills/`. Mỗi dòng có tác giả, giấy phép và trang Commons. Ảnh bị từ chối nằm trong `incoming/` và không được chọn. Chạy `python proof/kit-video-production-v1/library/build_v2_films.py catalog` để ghi lại catalog mà không viết lại hai video dài.

Biểu đồ nến không nằm trong catalog. Chúng được vẽ từ file OHLCV trong `market/`.

## Chọn ảnh cho một cảnh

`kit_video.library.choose_asset` nhận thương hiệu, nhóm và từ khóa. Ảnh đã dùng trong video đó bị bỏ. Ảnh cùng nhóm giống hoặc hash gần ảnh vừa dùng cũng bị bỏ. Không có ảnh đúng ý thì kết quả là `gap`, không lấy ảnh khác chủ đề.

## Nến

`market/BTCUSDT-1h.json` và `market/ETHUSDT-1h.json` là nến giờ công khai từ Binance, `mode: historical`. Không điền nến bịa. Cảnh `market_chart` đọc đúng file này và từ chối nếu symbol hoặc chế độ không khớp.
