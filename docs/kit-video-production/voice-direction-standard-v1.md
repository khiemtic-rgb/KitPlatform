# Voice Direction Standard V1

Tiêu chuẩn này thuộc KIT Video Production. Phim intro NOVIXA chỉ là nơi áp dụng đầu tiên. Nguồn máy đọc là `voice-direction-standard-v1.json`. Sheet của từng phim không chứa luật dùng chung.

Quy trình:

`Script → Voice Direction → TTS Generation → Audio Measurement → Listening Review → Timing Approval`

Lời kịch bản và chỉ dẫn diễn đọc là hai lớp. Chuỗi gửi TTS chỉ chứa lời cần đọc. `[nghỉ]` và `[nhấn mạnh]` không được nằm trong chuỗi đó.

## Danh tính giọng và cách diễn

Voice profile giữ giới tính, ngôn ngữ, vùng, nhà cung cấp, voice ID và phiên bản. Delivery preset giữ tốc độ, cao độ và âm lượng. Chọn giọng nam hay nữ không phải là chọn cách diễn.

Adapter hiện có là edge-tts 7.3.1. Nó nhận voice ID, `rate`, `volume`, `pitch` và loại boundary. Nó không nhận cảm xúc, nhấn từ, phiên âm IPA hay thẻ ngắt. Những mục đó là chỉ dẫn cho người nghe, không phải nút của adapter. Không áp một tốc độ cho mọi video. Ba preset `narration-clear-v1`, `narration-slower-v1` và `narration-faster-v1` là các lựa chọn, chưa được duyệt thành mặc định.

## Vai diễn

Một video có thể có một người dẫn, hai giọng đối thoại, hoặc nhiều vai. Mỗi dòng có thứ tự, speaker, profile, preset và lời. Pipeline không gắn cứng một giọng.

Catalog có `vi-VN-NamMinhNeural` ở trạng thái thử và `vi-VN-HoaiMyNeural` ở trạng thái chưa gọi. Vai trẻ em chưa có voice ID.

## Từ điển phát âm

Mỗi mục có chữ hiển thị, cách đọc mong muốn, chuỗi TTS, phạm vi và trạng thái kiểm chứng.

`VERIFIED` chỉ được ghi khi có người nghe duyệt. `UNVERIFIED` nghĩa là chưa có file thử. `PROBED` nghĩa là đã có file và số đo, chưa phải duyệt.

`NOVIXA` trên logo, phụ đề và UI giữ nguyên. Chỉ chuỗi TTS được phép dùng dạng đọc. Dạng đọc mục tiêu là Nô-vi-xa.

Người dùng đã nghe và duyệt `novixa-closed.wav`. Chuỗi TTS của chữ `NOVIXA` là `Nôvixa`. Logo, phụ đề và chữ trên hình vẫn là `NOVIXA`. Voice-read V3 dùng quy tắc này đã được nghe duyệt. Hình phim và thời lượng chưa khóa.

## Áp dụng NOVIXA

Sheet 12 cảnh nằm ở `proof/kit-video-production-v1/projects/novixa-intro-film/voice-direction-sheet.json`. Lời trên sheet phải trùng `script.md`. Nguồn âm thanh hiện tại là `20261023T084726Z-voice-read`. Hai bản đọc trước đó được giữ làm lịch sử.
