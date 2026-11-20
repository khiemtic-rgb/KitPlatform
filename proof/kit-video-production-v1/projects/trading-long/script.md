# trading-long

## segment s1-open
goal: Đọc giá trước khi nói về lệnh.
dialogue: |
  Đọc giá trước khi nói về lệnh.
on_screen:
- Agent Trading
- Đọc giá trước khi nói về lệnh.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 12
template_hint: brand_reveal

## segment s2-up
goal: BTCUSDT tăng 7,4% · khung 1 giờ
dialogue: |
  Bitcoin khung một giờ, đoạn này tăng 7,4 phần trăm. Đường trung bình được tính từ giá đóng cửa.
on_screen:
- Khối lượng
- BTCUSDT tăng 7,4% · khung 1 giờ
- Không phải khuyến nghị
- SMA 20
- Đoạn tăng
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 26
template_hint: market_chart

## segment s3-pull
goal: BTCUSDT có nhịp hồi trong đoạn tăng
dialogue: |
  Trong đoạn tăng có một nhịp hồi. Nhịp này lấy từ chính các nến, không phải hình vẽ thêm.
on_screen:
- Khối lượng
- BTCUSDT có nhịp hồi trong đoạn tăng
- Không phải khuyến nghị
- SMA 20
- Nhịp hồi
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 20
template_hint: market_chart

## segment s4-down
goal: BTCUSDT giảm 3,5% · khung 1 giờ
dialogue: |
  Cùng mã, một đoạn khác giảm 3,5 phần trăm. Vùng tô là mức giảm của giá, không phải lợi nhuận chiến lược.
on_screen:
- Khối lượng
- BTCUSDT giảm 3,5% · khung 1 giờ
- Không phải khuyến nghị
- SMA 20
- Mức giảm của giá
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 26
template_hint: market_chart

## segment s5-range
goal: ETHUSDT biên hẹp · RSI 14
dialogue: |
  Ethereum khung một giờ nằm trong biên hẹp. RSI được tính từ giá đóng cửa.
on_screen:
- Khối lượng
- ETHUSDT biên hẹp · RSI 14
- Không phải khuyến nghị
- RSI 14
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 22
template_hint: market_chart

## segment s6-macd
goal: BTCUSDT · MACD âm cuối cửa sổ
dialogue: |
  MACD của một đoạn Bitcoin khác, tính từ giá đóng cửa. Không phải tín hiệu vào lệnh.
on_screen:
- Khối lượng
- BTCUSDT · MACD âm cuối cửa sổ
- Không phải khuyến nghị
- MACD
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 20
template_hint: market_chart

## segment s7-agent
goal: Một nhịp của agent
dialogue: |
  Agent đọc dữ liệu, xét điều kiện, rồi mới nói tới rủi ro.
on_screen:
- Việc agent làm
- Sơ đồ, không phải lệnh
- Nguồn
- Xét
- Rủi ro
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 16
template_hint: designed_board

## segment s8-risk
goal: Agent Trading
dialogue: |
  Quan sát. Đánh giá. Đứng ngoài khi chưa đủ.
on_screen:
- Quan sát
- Agent Trading
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 18
template_hint: designed_board

## segment s9-close
goal: Lịch sử giá. Không phải khuyến nghị.
dialogue: |
  Lịch sử giá. Không phải khuyến nghị.
on_screen:
- Agent Trading
- Lịch sử giá. Không phải khuyến nghị.
acceptance:
- Chữ đúng câu đã khai.
duration_hint_seconds: 20
template_hint: brand_reveal
