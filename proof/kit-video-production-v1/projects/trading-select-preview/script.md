# trading-select-preview

## segment t-wide
goal: Cả đoạn giá
dialogue: |
  Người xem thấy hướng của cả cửa sổ.
on_screen:
- BTCUSDT khung một giờ
- Không phải khuyến nghị
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 5.057
template_hint: market_chart

## segment t-highlight
goal: Nến tăng mạnh nhất
dialogue: |
  Người xem thấy nến tạo đà, không phải một vùng tô ngẫu nhiên.
on_screen:
- Nến tăng trong vùng
- Không phải khuyến nghị
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 6.828
template_hint: market_chart

## segment t-sma
goal: SMA 20
dialogue: |
  Đường trung bình được tính từ giá đóng cửa của chuỗi.
on_screen:
- SMA 20 từ giá đóng cửa
- Không phải khuyến nghị
- SMA 20
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 6.069
template_hint: market_chart

## segment t-conclusion
goal: Con số của đoạn
dialogue: |
  Phần trăm là change của đúng cửa sổ được vẽ.
on_screen:
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 4.046
template_hint: kinetic_type
