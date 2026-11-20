"""Reviewer labels for the selector. These are judgments, not the program's output.

A still choice is appropriate when it shows the subject the sentence names, or when
no approved asset shows that subject and the result is an explicit gap. A screenshot
does not stand in for a room, a pack, or a person. A rejected asset or the wrong
brand is never appropriate. A chart is appropriate only when the returned window
meets the predicate on the stored candles, or when no window meets it and the
selector says so.
"""

from __future__ import annotations

REVIEW_CRITERION = (
    "Phù hợp khi ảnh được chọn đúng chủ thể của câu, hoặc khi thư viện không có ảnh đã duyệt "
    "cho chủ thể đó và kết quả là gap. Ảnh giao diện không thay cho không gian, bao bì hoặc người. "
    "Asset bị từ chối hoặc sai thương hiệu không được chọn. Chart phù hợp khi cửa sổ trả về "
    "thỏa điều kiện trên file nến, hoặc khi không có cửa sổ nào và bộ chọn nói rõ thiếu."
)


def _scene(scene_id: str, narration: str, **extra) -> dict:
    row = {
        "id": scene_id,
        "narration": narration,
        "brand": extra.pop("brand", "NOVIXA"),
        "start": extra.pop("start", 0),
        "end": extra.pop("end", 6),
        "gap": extra.pop("gap", False),
        "note": extra.pop("note", ""),
    }
    row.update(extra)
    return row


def groups() -> list[dict]:
    singles = [
        _scene("dashboard", "Màn tổng quan cho thấy nhịp của nhà thuốc.", accept=["nvx-doc008-dashboard"], reject=["nvx-qld-dashboard", "ref-pharmacy-floor"], note="Tổng quan nhà thuốc, không phải màn liên thông."),
        _scene("pos", "Bán hàng nằm trên màn hình quầy.", accept=["nvx-doc008-pos"], reject=["ref-pharmacy-floor"], note="Màn bán tại quầy."),
        _scene("stock", "Tồn kho và hạn dùng được nhìn trên màn hình.", accept=["nvx-doc008-stock"], reject=["ref-blister-foil", "nvx-qld-stock"], note="Màn tồn. Bản liên thông đang ở trạng thái review."),
        _scene("catalog", "Danh mục sản phẩm là danh sách trên phần mềm.", accept=["nvx-doc008-catalog"], reject=["ref-blister-foil", "ref-pack-still"], note="Danh sách phần mềm, không phải ảnh vỉ."),
        _scene("grn", "Nhập hàng có một màn riêng.", accept=["nvx-doc008-grn"], reject=["ref-pharmacy-floor"], note="Màn nhập, không phải liên thông."),
        _scene("crm", "Hồ sơ khách nằm trong phần mềm.", accept=["nvx-doc008-crm"], note="Hồ sơ khách."),
        _scene("reports", "Báo cáo mở từ màn báo cáo.", accept=["nvx-doc008-reports"], reject=["nvx-qld-revenue", "nvx-qld-nxt"], note="Màn báo cáo chung, không phải doanh thu hay xuất nhập tồn."),
        _scene("nxt", "Xuất nhập tồn là một báo cáo riêng.", accept=["nvx-qld-nxt"], reject=["nvx-doc008-dashboard"], note="Đúng báo cáo xuất nhập tồn."),
        _scene("revenue", "Doanh thu nằm ở báo cáo doanh thu.", accept=["nvx-qld-revenue"], reject=["nvx-doc008-reports"], note="Đúng báo cáo doanh thu."),
        _scene("app", "Khách có một màn ứng dụng riêng.", accept=["nvx-doc008-app"], note="Ứng dụng khách."),
        _scene("connect", "Liên thông dữ liệu thuốc là màn theo dõi kết nối.", accept=["nvx-qld-connect"], reject=["nvx-qld-sync"], note="Màn liên thông đã duyệt. Nhật ký đồng bộ đang review."),
        _scene("login", "Người dùng đăng nhập vào liên thông.", accept=["nvx-qld-login"], note="Màn đăng nhập."),
        _scene("draft", "Đơn nháp được giữ trên một màn.", accept=["nvx-doc008-draft"], note="Màn đơn nháp."),
        _scene("invoice", "Hóa đơn bán được mở riêng.", accept=["nvx-qld-sale"], note="Màn hóa đơn."),
        _scene("staff-pos", "Nhân viên có màn bán hàng riêng.", accept=["nvx-doc008-staff-pos"], reject=["nvx-doc008-pos"], note="Màn của nhân viên, không phải màn quầy chung."),
        _scene("drug-master", "Danh mục thuốc trên phần mềm liên thông.", accept=["nvx-qld-drug"], reject=["nvx-doc008-catalog", "ref-blister-foil"], note="Danh mục thuốc liên thông."),
        _scene("blister", "Đây là vỉ thuốc tham chiếu.", accept=["ref-blister-foil"], reject=["nvx-doc008-stock", "ref-pack-still"], note="Ảnh vỉ. Không lấy màn hình hay ảnh chai."),
        _scene("bottle", "Chai và lọ thuốc trong ảnh studio.", accept=["ref-pack-still"], reject=["ref-blister-foil"], note="Ảnh chai và lọ."),
        _scene("floor", "Không gian tủ thuốc của nhà thuốc bán lẻ.", accept=["ref-pharmacy-floor"], reject=["ref-pharmacy-wait", "nvx-doc008-pos", "s01-pharmacy"], note="Nhà thuốc bán lẻ, không phải phòng chờ hay ảnh bị từ chối."),
        _scene("waiting", "Phòng chờ trước quầy thuốc bệnh viện.", accept=["ref-pharmacy-wait"], reject=["ref-pharmacy-floor"], note="Phòng chờ."),
        _scene("hands", "Người đang kiểm một vỉ trên bàn.", accept=["ref-hands-blister"], reject=["ref-blister-foil"], note="Thao tác kiểm vỉ, không phải ảnh vỉ không có người."),
        _scene("pharmacist", "Dược sĩ tư vấn khách trước quầy.", gap=True, reject=["ref-hands-blister", "ref-pharmacy-brochure", "nvx-doc008-pos"], note="Không có ảnh dược sĩ đã duyệt."),
        _scene("warehouse", "Nhân viên kiểm hàng trong kho thật.", gap=True, reject=["nvx-doc008-grn"], note="Màn nhập không phải ảnh kho."),
        _scene("retail-box", "Hộp thuốc bán lẻ chụp đủ các mặt.", gap=True, reject=["ref-blister-foil", "pkg-vitamin-c"], note="Vỉ không thay cho hộp đủ mặt. Thẻ mockup đã bị từ chối."),
        _scene("desk", "Bàn giao dịch với nhiều màn hình đang có biểu đồ.", brand="AgentTrading", gap=True, reject=["ref-gaming-monitors", "nvx-doc008-dashboard"], note="Không có ảnh bàn giao dịch đã duyệt."),
        _scene("logo", "Logo Novixa ở đầu video.", accept=["logo-novixa"], note="Logo gốc."),
        _scene("wrong-brand", "Không gian tủ thuốc của nhà thuốc bán lẻ.", brand="AgentTrading", gap=True, reject=["ref-pharmacy-floor"], note="Ảnh nhà thuốc thuộc NOVIXA, không đưa sang Agent Trading."),
        _scene("no-subject", "Hôm nay trời đẹp.", gap=True, note="Câu không có chủ thể hình. Không lấy ảnh ngẫu nhiên."),
        _scene("btc-rise", "Bitcoin khung một giờ có một đoạn tăng rõ.", brand="AgentTrading", chart={"situation": "rise", "symbol": "BTCUSDT", "min_change": 0.03, "count": 48}, note="Cửa sổ phải tăng thật trên file BTC."),
        _scene("eth-narrow", "Ethereum khung một giờ nằm trong biên hẹp.", brand="AgentTrading", chart={"situation": "narrow", "symbol": "ETHUSDT", "max_change": 0.012, "count": 48}, note="Biên hẹp trên file ETH."),
        _scene("btc-drop", "Bitcoin khung một giờ có một đoạn giảm.", brand="AgentTrading", chart={"situation": "drop", "symbol": "BTCUSDT", "min_change": 0.03, "count": 48}, note="Đoạn giảm trên file BTC."),
        _scene("btc-impossible", "Bitcoin khung một giờ phải tăng ít nhất 80 phần trăm.", brand="AgentTrading", gap=True, chart={"situation": "rise", "symbol": "BTCUSDT", "min_change": 0.8, "count": 48}, note="File không có đoạn tăng 80 phần trăm."),
        _scene("strategy", "Chiến lược giao dịch này đã có lợi nhuận.", brand="AgentTrading", gap=True, chart={"situation": "strategy_pnl"}, note="Không vẽ lợi nhuận chiến lược."),
    ]
    sequence = [
        {
            "groupId": "pos-then-staff",
            "scenes": [
                _scene("seq-pos", "Bán tại quầy trên phần mềm.", accept=["nvx-doc008-pos"], note="Màn quầy trước."),
                _scene("seq-staff", "Sau đó mở màn bán của nhân viên.", start=6, end=12, accept=["nvx-doc008-staff-pos"], reject=["nvx-doc008-pos"], note="Màn nhân viên, không lặp màn quầy."),
            ],
        },
        {
            "groupId": "pack-then-compare",
            "scenes": [
                _scene("seq-blister", "Cho xem vỉ thuốc tham chiếu.", accept=["ref-blister-foil"], note="Ảnh vỉ."),
                _scene("seq-compare", "Giữ vỉ này cạnh màn tồn kho để đối chiếu.", start=6, end=12, accept_all=["nvx-doc008-stock", "ref-blister-foil"], note="Được dùng lại vỉ vì câu nói giữ nó cạnh màn tồn."),
            ],
        },
        {
            "groupId": "stock-then-other",
            "scenes": [
                _scene("seq-stock", "Tồn kho và hạn dùng trên màn hình phần mềm.", accept=["nvx-doc008-stock"], note="Màn tồn."),
                _scene("seq-other-stock", "Không dùng lại màn vừa rồi. Cần bản tồn liên thông khác.", start=6, end=12, gap=True, reject=["nvx-doc008-stock", "nvx-qld-stock"], note="Bản liên thông chưa được duyệt. Không dùng lại màn vừa chọn."),
            ],
        },
    ]
    return [{"groupId": scene["id"], "scenes": [scene]} for scene in singles] + sequence
