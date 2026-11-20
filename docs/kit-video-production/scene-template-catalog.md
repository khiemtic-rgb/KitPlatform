# Catalog template cảnh

Template nhận `params`, palette và thời lượng của cảnh. Không sửa code lõi để đổi chữ, màu hay số giây. Cảnh đặc biệt dùng `templateId` chưa đăng ký sẽ bị chặn, để người thiết kế riêng.

| templateId | Việc diễn ra | Params chính |
| --- | --- | --- |
| `kinetic_type` | Từng dòng chữ hiện và trượt lên | `lines[]`: `text`, `at`, `scale` |
| `object_move` | Vật đi từ điểm này sang điểm khác, có thể rơi | `objects[]`: `from`, `to`, `start`, `end`, `w`, `h`, `color`, `label`, `ease`; `caption` |
| `object_transform` | Một khối đổi màu, đổi độ cao và hạ xuống | `beats.transform`, `caption` |
| `character_entrance` | Người hình học đi vào và với tới hộp | `toX`, `enterEnd`, `propX`, `caption` |
| `calendar_flip` | Tờ lịch lật tại các mốc. `scale` và `at` mặc định giữ bố cục cũ. | `flips[]`, `caption`, `scale`, `at` |
| `data_connection` | Nút hiện rồi đường nối mọc | `nodes[]`, `links[]`, `caption` |
| `ui_showcase` | Ảnh UI thật nằm trọn trong khung, có nhãn demo nếu cần | `assetId` trỏ asset `product_ui` |
| `brand_reveal` | Logo file nếu có, không thì chữ `wordmark` | `logoAssetId`, `wordmark`, `line` |
| `end_card` | Như brand reveal, thêm `url` | `wordmark`, `line`, `url`, `logoAssetId` |
| `metaphor_locked_capital` | Hộp vào kệ, hộp bán nhanh rời đi, lịch lật, hộp còn lại thành khối rơi xuống két, một đồng không ghi số đi trước. Bản V1, giữ để dự án cũ vẫn dựng được. | `boxCount`, `fastCount`, `beats`, `headlineAt` |
| `stock_story` | Cùng ý vốn bị khóa, nhưng là sân khấu có chiều sâu: người có khớp, thùng có mặt, kệ và két. `mode` là `acting`, `metaphor` hoặc `hybrid`. | `mode`, `headline`, `boxCount`, `fastCount`, `beats`, `headlineAt`. Màu người/gỗ/tường lấy từ palette nếu có. |
| `reference_stage` | Ghép ảnh đã đăng ký. Một lớp có thể nhấc theo cung rồi dừng. Chữ Việt vẽ bằng font dự án, không nằm sẵn trong file ảnh. | `stageFile`, cùng các khóa mà file sân khấu ghi trong ngoặc nhọn, ví dụ `productLabel`, `message`. Màu thẻ lấy từ palette. |
| `plate_camera` | Reframe một ảnh đã đăng ký: giữ nguyên, đẩy vào, kéo ra, lia ngang, lia dọc hoặc zoom nhẹ. Chỉ dùng pixel có sẵn. `amount` lớn hơn 0,12 bị chặn vì ảnh không đủ trường nhìn. | `assetId`, `preset` (`hold`, `push_in`, `pull_out`, `pan`, `tilt`, `zoom`), `amount`, `caption` |
| `designed_board` | Bố cục thương hiệu dùng chung. Chữ và số liệu đến từ params. | `kind`: `hero_still`, `portrait_split`, `brand_center`, `brand_split`, `end_lockup`, `app_screen`, `manifesto`, `workspace`, `lanes`, `catalog`, `dashboard`, `triptych` |

`beats` là cặp giây `[bắt đầu, kết thúc]` trong cảnh. Nếu không khai, template chia theo tỷ lệ của `durationSeconds`, nên cùng một cảnh vẫn chạy khi thời lượng đổi.

`pipelineTestLabel` vẽ một dòng nhỏ trong vùng an toàn. Chỉ các fixture kiểm thử đặt dòng `DỰ ÁN KIỂM THỬ`. Style proof NOVIXA không đặt dòng này.

Tọa độ trong `object_move`, `data_connection` và nhân vật là tỷ lệ khung, nên cùng params chạy ở 16:9 và 9:16. Bố cục đẹp hay không vẫn là việc của người xem từng tỷ lệ.

Âm thanh không phải một template hình. Shot plan đặt SFX tại `atSeconds`. Khi có lời và `ducking: true`, mix hạ SFX bằng `sidechaincompress` lúc có giọng. Không có lệnh `atempo`.
