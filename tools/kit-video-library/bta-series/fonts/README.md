# Font kênh BTA

Tất cả dùng giấy phép SIL Open Font License 1.1 (OFL): dùng, nhúng vào video, sửa, phân phối lại đều được; không bán riêng font.

| File | Gốc | Dùng cho |
| --- | --- | --- |
| BTA-Display.ttf | Fraunces (Google Fonts), bản tĩnh wght 700, opsz 72, SOFT 50 | Tiêu đề, chữ to trên hình |
| BTA-Sans-Regular.ttf, BTA-Sans-Bold.ttf | Inter (Google Fonts), bản tĩnh wght 450 / 750 | Nhãn, chú thích (đủ dấu tiếng Việt) |
| BTA-Hanzi.otf | Noto Serif CJK Bold, cắt gọn chỉ các chữ trong BTA-Hanzi.chars.txt | Chữ Hán trên hình (卯, 兔, 12 chi...) |

Cần chữ Hán mới: thêm vào BTA-Hanzi.chars.txt rồi cắt lại từ Noto Serif CJK (fontTools: `pyftsubset NotoSerifCJK-Bold.ttc --font-number=0 --text-file=BTA-Hanzi.chars.txt --output-file=BTA-Hanzi.otf`).
