# Giới hạn và phần chưa làm

- `stock_story` có người khớp và thùng có khối, nhưng chưa có miệng nói, chưa có xương đầy đủ, và chưa có nét họa sĩ. Proof V1 giữ template cũ.
- `reference_stage` ghép ảnh cắt nền. Prototype nhà thuốc có dược sĩ bán thân một tư thế, không có tay cầm hộp, không có xương. Hộp, lọ và vỉ là ảnh riêng. Chữ do engine vẽ.
- Quảng cáo image-to-video chưa chạy được trên máy này: thiếu video tham chiếu, preview v7 và bộ chuẩn `visual-standard-v1` còn `AWAITING_REVIEW`, và thiếu `RUNWAY_API_KEY` / `VEO_JOIN_FILE` / `FAL_KEY`. Cảnh lấy hộp `s02-take-box-probe` mới là đặc tả. Chưa có tư thế tay. Thẻ chữ, nhãn mockup và bảng số không thay cho một cảnh video.
- Chưa có nhạc được cấp quyền. `musicPolicy: required` sẽ chặn render. Pilot để `none`.
- SFX trong thư viện là tiếng tổng hợp để thử đồng bộ. Không phải thiết kế âm thanh cuối.
- `analyze` ước lượng bằng số ký tự, không đo giọng. Số đó không được đem đi khóa timing.
- edge-tts cần mạng. Adapter chỉ có voice ID, tốc độ, cao độ và âm lượng. Không có nút cảm xúc, nhấn từ hay IPA. Voice-read V3, cách đọc `Nôvixa`, nghỉ câu hỏi ở s04 và câu `novixa chấm vn` ở s12 đã được nghe duyệt. Hình phim chưa duyệt và thời lượng chưa khóa.
- Không có ASR tiếng Việt. QA không đối chiếu lời nghe được với kịch bản. Spectrogram và mốc từ không thay cho người nghe.
- `ui_showcase` chỉ chạy khi asset là `product_ui` đã duyệt. Ba ảnh NOVIXA đang để `reference` / `proposed`, nên các cảnh bán hàng, tồn kho và báo cáo chưa dựng.
- Cảnh mở đầu nhà thuốc, cảnh bận rộn và cảnh triết lý đang `templateId: custom`. Chưa có hình cho chúng.
- Chưa xuất cùng lúc nhiều tỷ lệ từ một lần bấm. Đổi `format` và render lần nữa. Mỗi lần là một thư mục.
- Chưa có contact sheet thay cho xem video. Sheet chỉ ghép các khung đã lưu.
- Chưa phát hành, chưa commit, chưa push, chưa deploy.
- Phim NOVIXA đầy đủ chưa được dựng. Style proof không phải bản hoàn thành.
- Hash trong `render-log.json` là manifest và shot plan tại lúc encode. Sửa ghi chú pipeline sau đó làm file hiện tại khác hash đã lưu.
- Kiểm thử tự động nằm ở `tools/kit-video-production/tests/test_system.py`: 20 test, gồm render ngắn, `stock_story`, `reference_stage`, lát quảng cáo bị chặn khi thiếu credential, bảng số minh họa khóa từ `demoData`, đối chiếu bộ chuẩn hình, probe nhận diện, storyboard, shot plan 25 giây còn `proposed`, phim intro không bị ép về gợi ý 3 phút, và chuẩn giọng không được tự duyệt. Test không chứng minh hình đẹp, cách đọc thương hiệu, hay chuyển động tự nhiên.
