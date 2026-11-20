# Kiểm tra khả năng hoạt hình cục bộ — POC 002.1

Ngày: 2026-10-22. Không tạo video. Không tải, không cài, không gọi API. Không sửa asset hay mã nguồn.

## Kết luận

`BLOCKED — NO VERIFIED LOCAL ANIMATION TOOL`

Máy này ghép được ảnh tĩnh. Không có công cụ nào đã cài, chạy offline, và có dữ liệu cần thiết để tạo chuyển động nhân vật 3D, hoạt ảnh 2D theo lớp, hay video từ model AI cục bộ.

## Đã đọc

- `STYLIZED-PIPELINE-REPORT.md`: lượt đầu không có Blender hay weight. Lượt sau dựng `job/output.mp4` bằng crop trên ảnh tĩnh.
- `RENDER-REPORT.md`: video 15 giây, 1080×1920, kết luận `NEEDS REVIEW`. Cảnh sổ là ảnh `scene2-ledger.jpg` được trượt khung hình. Bút không viết.

## Bằng chứng trên máy

Lệnh chạy trong phiên này, không dựa vào báo cáo cũ.

| Kiểm tra | Kết quả |
|---|---|
| `ffmpeg -version` | 8.1.2 full build, có trên PATH. Có filter `zoompan`, `overlay`, `minterpolate` |
| `ffprobe` | Có trên PATH, cùng bản build |
| `python` | 3.14.6 tại `C:\Python314\python.exe` |
| Pillow, scikit-image, edge-tts | Import được. Xử lý ảnh và tiếng, không có xương nhân vật |
| onnxruntime 1.27.0 | Import được. Không có thư mục weight |
| torch, diffusers, bpy, moderngl, trimesh, pyrender, OpenGL, pygame, manim, cv2 | Không có module |
| `blender`, `krita`, `opentoonz`, `cascadeur`, `unity`, `UnrealEditor`, `ollama`, `comfy` | Không có trên PATH |
| `C:\Program Files\Blender Foundation`, Blackmagic, Adobe, Unity, Epic, OpenToonz, Krita | Không có thư mục |
| Tên thư mục trong Program Files và Program Files (x86) khớp Blend, Toonz, Krita, Unity, Unreal, Adobe, Cascadeur, Spine | Không thấy |
| `~\.cache\huggingface`, `~\.cache\torch`, `%LOCALAPPDATA%\huggingface`, `~\.ollama` | Không có |
| ComfyUI / Stable Diffusion ở thư mục thường gặp trên C: và E: | Không có |
| `assets/scene2-ledger.jpg` | JPEG RGB, 720×1280, 1 lớp, 239.762 byte |

## Cái gì sẵn sàng, cái gì chỉ là phần mềm trống

Sẵn sàng chạy offline, không cần tải thêm:

- ffmpeg và Pillow làm **chuyển động camera trên ảnh tĩnh**: zoom, lia, crop. POC 002 đã dùng cách này. `minterpolate` chỉ nội suy khung giữa hai hình có sẵn, không tự nghĩ ra tay đang viết.
- edge-tts đọc lời. Không liên quan đến hình chuyển động.

Không sẵn sàng, dù nghe gần:

- **Hoạt ảnh 2D theo lớp.** Pillow ghép được nhiều lớp nếu đã có file tay, bút, trang sổ tách riêng. `scene2-ledger.jpg` là một JPEG phẳng. Không có Spine, OpenToonz hay Krita.
- **Hoạt ảnh nhân vật 3D.** Không có Blender, Unity, Unreal, Cascadeur, và không có `bpy`. Không có file rig. Ảnh JPG không phải mesh.
- **Video bằng model AI cục bộ.** onnxruntime là runtime không kèm model. Không có torch, diffusers, weight, Ollama hay ComfyUI.

## Chi phí và việc phải tải

Lượt này: chi phí 0. Không tải model, không cài thư viện.

Muốn có chuyển động nhân vật thì phải thêm một thứ chưa có trên máy, ví dụ Blender kèm file nhân vật có xương, hoặc một model video kèm weight. Cả hai đều là cài đặt hoặc tải thêm. Chưa được phép trong lượt này, và chưa có bằng chứng là miễn phí cho đến khi người phụ trách chỉ định đúng gói.

## Bước tiếp theo

Chưa có phương án thử 3–4 giây cảnh viết sổ. Không dùng slideshow hay zoom ảnh `scene2-ledger.jpg` để thay.

Bước duy nhất có ích: người phụ trách đặt lên máy một công cụ offline đã có xương hoặc weight, rồi mới mở một lượt thử riêng. Ảnh `scene2-ledger.jpg` chỉ dùng làm tham chiếu khuôn mặt, áo và góc sổ. Không biến JPEG đó thành người đang viết.
