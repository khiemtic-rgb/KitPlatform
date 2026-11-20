# Dựng video dài tập 01 (14/11/2026)

Thứ tự chạy (đường dẫn trong script đang trỏ /home/claude/..., đổi sang E:\KitPlatform khi chạy trên máy):
1. `align2.py` (+ `psa.py`): căn mốc từng chữ của giọng 1b, 2a, 3a bằng pocketsphinx (pip install pocketsphinx). Ra `lines2.json`.
2. `timeline.py`: ghép giọng, chèn khoảng nghỉ (cảnh 1 theo nhịp bản v6; cảnh 2–11 nghỉ 0,5 giây giữa câu, 1,05 giây giữa cảnh). Ra `timeline.json`, `voice_full.wav`, `scene1_anchor.json`.
3. `render_dai.py <từ giây> <đến giây> <ra.mp4> 30`: dựng hình. Cảnh 1 dùng `canh1_dai.py` (bản canh1_moi.py đổi nhịp qua `nhip_dai.py`, "few minutes"); cảnh 2–11 dùng edu_studio + nv_board; thẻ kết 12 giây.
4. `mix_dai.py`: giọng + nhạc nối vòng (vietnam-lofi/asian-lofi, tự nhỏ khi có giọng) + tiếng động, về −14 LUFS.

Thay đổi bộ dựng đi kèm: `bta_art.py` (ảnh `fit: contain`, `grade: false` cho tranh thật), `novixa_board.py` (`bleed: true` = ảnh phủ kín khung ngang; chỉ áp dụng khi ghi rõ, không ảnh hưởng Novixa), `.hinh.json` (ảnh đã chọn c06/c07/c08/c09, ảnh phủ kín), `c07-dong-ho-dam-cuoi-chuot.vung.json` (vùng phóng trên tranh thật).
