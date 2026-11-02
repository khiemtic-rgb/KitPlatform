# kit-video-production

Công cụ dựng video theo manifest. Tài liệu chuẩn nằm ở [docs/kit-video-production](../../docs/kit-video-production/README.md).

```text
python kv.py validate <project-dir>
python kv.py analyze <script.md> --project-id <id> --out <file>
python kv.py render <project-dir> --repo <repo-root> [--scenes id] [--allow-style-proof] [--preview-only]
python kv.py qa <render-dir> --project <project-dir>
python kv.py voice --text "..." --out <file.mp3>
python kv.py sfx <dest-dir>
```

`voice` gọi edge-tts ở tốc độ `+0%`. Không có tham số làm giọng đọc nhanh hơn.
