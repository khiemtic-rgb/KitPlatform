# NOVIXA INTRO V1 — Cổng nghe giọng

Trạng thái: **READY FOR OWNER LISTENING**

Chưa được duyệt để sản xuất. Chưa có voice-over đầy đủ và chưa có video.

## File cần nghe

`proof/novixa-intro-preprod-v1/voice-approval/novixa-pronunciation-sample.mp3`

MP3, 24 kHz, mono, 3,864 giây, 23.184 byte.

Mở file bằng trình phát trên máy. Nghe một lần cả câu. Duyệt khi tên thương hiệu nghe là “nô vi xa” liền một nhịp, không đánh vần N-O-V-I-X-A, không tách thành ba nhịp, và không bị nhấn mạnh hơn các từ còn lại. Nếu chưa đúng, trả lại và chưa dựng.

## Câu và cấu hình

Câu đưa vào máy đọc:

“Nôvixa là nền tảng vận hành nhà thuốc thông minh.”

Giọng `vi-VN-NamMinhNeural`. Tốc độ `+0%`, cao độ `+0Hz`, âm lượng `+0%`. Công cụ `edge-tts` sẵn có.

Chữ này chỉ nằm trong file âm thanh. Trên hình và trên logo vẫn viết `NOVIXA`.

## Vì sao không đưa thẳng chữ NOVIXA vào máy đọc

Cùng một câu, biên từ của engine cho kết quả:

| Cách viết đưa vào máy | Token | Độ dài token |
| --- | --- | --- |
| NOVIXA | một token `NOVIXA` | 1,30 giây |
| Nô-vi-xa | một token `Nô-vi-xa` | 0,88 giây |
| Nôvixa | một token `Nôvixa` | 0,91 giây |

Token `Nôvixa` kết thúc đúng lúc từ “là” bắt đầu, không có khoảng trống ở biên từ. Bản có gạch nối cũng là một token, nhưng file mẫu dùng dạng dính “Nôvixa” để máy không có ký tự ngắt giữa âm tiết.

File `proof/novixa-motion-pilot-001/assets/voice/probe-novixa.mp3` giữ nguyên, 11.376 byte. Không ghi đè.

## Chưa xác nhận bằng tai

Môi trường này không nghe được file. Số đo biên từ không chứng minh nguyên âm đã đúng “nô vi xa”. Chỉ người nghe mới chốt.

`SCRIPT-REVIEW.md`, logo và các file âm thanh cũ không bị sửa.
