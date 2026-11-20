# trading-beat-proof

## segment s1-wide
goal: BTCUSDT khung một giờ
dialogue: |
  Bitcoin khung một giờ, nhìn cả đoạn tăng.
on_screen:
- BTCUSDT khung một giờ
- Không phải khuyến nghị
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: market_chart

## segment s2-region
goal: Vùng đang xét
dialogue: |
  Thu hẹp vào vùng có nến tăng mạnh.
on_screen:
- Vùng đang xét
- Không phải khuyến nghị
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: market_chart

## segment s3-highlight
goal: Nến tăng trong vùng
dialogue: |
  Đánh dấu đúng nến tăng trong vùng đó.
on_screen:
- Nến tăng trong vùng
- Không phải khuyến nghị
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: market_chart

## segment s4-sma
goal: SMA 20 từ giá đóng cửa
dialogue: |
  Đường trung bình dùng hai mươi giá đóng cửa, kể cả nến trước đoạn này.
on_screen:
- SMA 20 từ giá đóng cửa
- Không phải khuyến nghị
- SMA 20
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: market_chart

## segment s5-close
goal: Kết luận đúng số đã tính
dialogue: |
  Đoạn này tăng 7,4 phần trăm. Không phải khuyến nghị.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5
template_hint: kinetic_type
