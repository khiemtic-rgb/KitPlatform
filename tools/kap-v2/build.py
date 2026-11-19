# -*- coding: utf-8 -*-
"""Sinh bộ câu hỏi PHARMACY_V2 cho survey.novixa.vn.

Một nguồn duy nhất -> 3 file:
  PHARMACY_V2.template.json   (cùng cấu trúc với API /templates/PHARMACY_V1)
  PHARMACY_V2.insights.json   (luật báo cáo: phát hiện, việc tuần này, ước tính tiền)
  PHARMACY_V2.review.md       (bản đọc để dược sĩ / sales duyệt câu chữ)
Chạy: python3 build.py
"""
import json, uuid, sys
from collections import Counter

NS = uuid.UUID("6f1c2a52-7d0e-4c5e-9a43-5b2d1e0c0a11")
TEMPLATE_CODE = "PHARMACY_V2"
VERSION = "2.0"


def uid(*parts):
    return str(uuid.uuid5(NS, "/".join([TEMPLATE_CODE, *parts])))


def opt(code, label, score):
    return {"code": code, "label": label, "score": score}


def S(*labels_scores):
    """Phương án tính điểm 1..4 theo thứ tự; phần tử (label, None) = 'không tính điểm'."""
    out, n = [], 0
    for item in labels_scores:
        if isinstance(item, tuple):
            label, score = item
        else:
            n += 1
            label, score = item, n
        code = "UNKNOWN" if score is None else f"OPT{score}"
        out.append(opt(code, label, score))
    return out


def N(*pairs):
    """Phương án không tính điểm: (code, label)."""
    return [opt(c, l, None) for c, l in pairs]


UNK = lambda label="Không rõ": (label, None)

# ---------------------------------------------------------------------------
# BỘ CÂU HỎI
# q(code, title, options, help=None, scorable=True, meta=None, impact=1.0)
# impact: trọng số ảnh hưởng dùng để xếp hạng 3 việc nên làm.
# ---------------------------------------------------------------------------
def q(code, title, options, help=None, scorable=True, meta=None, impact=1.0):
    return dict(code=code, title=title, options=options, help=help,
                scorable=scorable, meta=meta or {}, impact=impact)


def when(pain):
    # frontend survey-logic.ts đọc metadata.showIf.{questionCode, optionCode}
    return {"showIf": {"questionCode": "P3", "optionCode": pain}}


CATEGORIES = [
    ("PROFILE", "Bắt đầu", [
        q("P2", "Nhà thuốc của anh/chị có mấy cơ sở?",
          N(("SITES_1", "1 cơ sở"), ("SITES_2_3", "2–3 cơ sở"),
            ("SITES_4_5", "4–5 cơ sở"), ("SITES_6P", "Trên 5 cơ sở")),
          scorable=False, meta={"role": "segment"}),
        q("P3", "Việc nào đang làm anh/chị đau đầu nhất lúc này?",
          N(("PAIN_CUSTOMER", "Khách ít quay lại"),
            ("PAIN_INVENTORY", "Hàng tồn, cận hạn, hết hàng"),
            ("PAIN_STAFF", "Nhân viên làm không đều tay"),
            ("PAIN_NUMBERS", "Không nắm rõ lãi lỗ"),
            ("PAIN_COMPLIANCE", "Sổ sách, lo khi có kiểm tra"),
            ("PAIN_GROWTH", "Muốn mở thêm nhưng chưa sẵn sàng")),
          help="Chọn một việc. Phần sau sẽ hỏi kỹ hơn đúng việc này.",
          scorable=False, meta={"leadTag": "pain_point"}),
        q("P5", "Mỗi đơn khách trả trung bình khoảng bao nhiêu?",
          N(("AOV_LT80", "Dưới 80.000đ"), ("AOV_80_150", "80.000–150.000đ"),
            ("AOV_150_300", "150.000–300.000đ"), ("AOV_GT300", "Trên 300.000đ"),
            ("UNKNOWN", "Không rõ")),
          help="Ước lượng là được.",
          scorable=False, meta={"role": "input"}),
    ]),
    ("CUSTOMER", "Khách hàng", [
        q("C1", "Trong 10 khách gần nhất, khoảng mấy người là khách quen?",
          N(("REG_0_2", "0–2 người"), ("REG_3_5", "3–5 người"),
            ("REG_6_8", "6–8 người"), ("REG_9_10", "9–10 người"),
            ("UNKNOWN", "Không để ý")),
          help="Không có đáp án đúng sai. Nhà thuốc gần bệnh viện thường ít khách quen hơn nhà thuốc trong khu dân cư.",
          scorable=False, meta={"role": "input"}),
        q("C2", "Khách quen quay lại, nhân viên biết lần trước họ mua gì bằng cách nào?",
          S("Không biết — phải hỏi lại khách",
            "Nhân viên nhớ mặt, nhớ thuốc",
            "Tra sổ hoặc tìm trong máy, mất vài phút",
            "Nhập số điện thoại là thấy ngay"), impact=1.0),
        q("C3", "Khách dùng thuốc lâu dài (huyết áp, tiểu đường…) sắp hết thuốc, nhà thuốc có nhắc không?",
          S("Không nhắc",
            "Chỉ khi khách nhờ",
            "Nhắc vài khách quen qua điện thoại, Zalo",
            "Có danh sách, nhắc đều theo lịch"), impact=1.3),
        q("C4", "Tháng trước, nhà thuốc chủ động hỏi thăm lại khoảng bao nhiêu khách sau khi bán?",
          S("Không ai", "Vài khách", "Khoảng 10–30 khách", "Trên 30 khách",
            UNK("Không theo dõi")), impact=0.9),
        q("C5", "Anh/chị có biết tháng này bao nhiêu khách mới, bao nhiêu khách cũ quay lại không?",
          S("Không biết", "Chỉ có cảm giác chung",
            "Đếm được nếu ngồi lục sổ", "Xem được con số ngay"), impact=0.8),
        q("C6", "Theo anh/chị, khách không quay lại chủ yếu vì sao?",
          N(("WHY_PRICE", "Giá cao hơn chỗ khác"),
            ("WHY_CONVENIENCE", "Chỗ khác tiện đường hơn"),
            ("WHY_FORGET", "Không được nhắc nên quên"),
            ("WHY_STOCKOUT", "Lúc cần thì nhà thuốc hết hàng"),
            ("UNKNOWN", "Không rõ")),
          scorable=False, meta=when("PAIN_CUSTOMER")),
    ]),
    ("INVENTORY", "Kho", [
        q("I1", "Lần gần nhất anh/chị phát hiện một lô sắp hết hạn là nhờ đâu?",
          S("Tình cờ lúc lấy hàng cho khách",
            "Lúc kiểm kê",
            "Rà sổ theo lịch định kỳ",
            "Phần mềm báo trước",
            UNK("Chưa gặp trường hợp này")), impact=1.2),
        q("I2", "Tháng trước, tiền hàng hết hạn phải hủy hoặc trả lại khoảng bao nhiêu?",
          N(("EXP_0", "Gần như không có"), ("EXP_LT1M", "Dưới 1 triệu"),
            ("EXP_1_3M", "1–3 triệu"), ("EXP_3_7M", "3–7 triệu"),
            ("EXP_GT7M", "Trên 7 triệu"), ("UNKNOWN", "Không rõ")),
          help="Ước lượng là được.",
          scorable=False, meta={"role": "input"}),
        q("I3", "Tuần trước, khoảng bao nhiêu lần khách hỏi mua mà nhà thuốc hết hàng?",
          S("Trên 15 lần", "6–15 lần", "2–5 lần", "0–1 lần",
            UNK("Không để ý")), impact=1.2),
        q("I4", "Khi đặt hàng, anh/chị quyết định số lượng dựa vào đâu?",
          S("Thấy vơi thì nhập",
            "Theo kinh nghiệm và gợi ý của trình dược viên",
            "Xem lại lượng bán tháng trước",
            "Có mức tồn tối thiểu cho từng mặt hàng"), impact=1.0),
        q("I5", "Lần kiểm kê toàn bộ kho gần nhất là khi nào?",
          S("Chưa bao giờ hoặc không nhớ", "Hơn 6 tháng trước",
            "Trong 1–6 tháng qua",
            "Trong tháng này, hoặc kiểm từng nhóm hàng đều đặn"), impact=0.8),
        q("I6", "Anh/chị có kể ngay được 5 mặt hàng bán chậm nhất đang giữ vốn không?",
          S("Không", "Đoán được", "Có, nhưng phải lục sổ", "Có, xem được ngay"),
          impact=0.9),
        q("I7", "Hàng cận hạn hiện được xử lý thế nào?",
          N(("NEAR_WAIT", "Để đến khi hết hạn rồi hủy"),
            ("NEAR_RETURN", "Trả nhà cung cấp nếu được"),
            ("NEAR_PUSH", "Giảm giá, ưu tiên bán trước"),
            ("NEAR_NONE", "Chưa có cách làm thống nhất")),
          scorable=False, meta=when("PAIN_INVENTORY")),
    ]),
    ("OPERATIONS", "Vận hành", [
        q("O1", "Nếu anh/chị nghỉ 3 ngày, nhà thuốc chạy thế nào?",
          S("Phải đóng cửa hoặc bán rất hạn chế",
            "Vẫn mở nhưng nhân viên gọi hỏi liên tục",
            "Chạy được, vài việc phải chờ anh/chị về",
            "Chạy bình thường"), impact=1.1),
        q("O2", "Nhân viên mới mất bao lâu thì tự đứng quầy được?",
          S("Trên 2 tháng", "1–2 tháng", "2–4 tuần",
            "Dưới 2 tuần — có tài liệu và người kèm",
            UNK("Chưa từng tuyển nhân viên")), impact=0.8),
        q("O3", "Cùng một bệnh thông thường như cảm cúm, các nhân viên có tư vấn giống nhau không?",
          S("Mỗi người một kiểu", "Gần giống nhau, do quen tay",
            "Có hướng dẫn chung nhưng chưa đủ",
            "Có hướng dẫn cho các bệnh hay gặp",
            UNK("Chỉ mình tôi đứng quầy")), impact=0.9),
        q("O4", "Cuối ngày, tiền thu có khớp với số đã bán không?",
          S("Thường lệch, không rõ vì sao", "Đôi khi lệch",
            "Ít lệch, tìm được nguyên nhân",
            "Khớp, có đối chiếu hằng ngày"), impact=1.0),
        q("O5", "Các việc định kỳ (ghi nhiệt độ, độ ẩm, vệ sinh, sổ sách) có người phụ trách và ghi lại không?",
          S("Chưa làm đều", "Làm khi sắp có kiểm tra",
            "Làm đều nhưng hay thiếu sổ",
            "Có người phụ trách, ghi đủ hằng ngày"),
          meta={"review": "pharmacist"}, impact=1.1),
        q("O6", "Lần kiểm tra gần nhất, nhà thuốc bị nhắc ở phần nào?",
          N(("INSP_NONE_YET", "Chưa bị kiểm tra lần nào"),
            ("INSP_RECORDS", "Sổ sách, ghi chép"),
            ("INSP_STORAGE", "Bảo quản, sắp xếp thuốc"),
            ("INSP_STAFF", "Hồ sơ nhân sự, bằng cấp"),
            ("INSP_OK", "Không bị nhắc gì")),
          scorable=False, meta={**when("PAIN_COMPLIANCE"), "review": "pharmacist"}),
        q("O7", "Việc nào với nhân viên làm anh/chị mệt nhất?",
          N(("STAFF_TURNOVER", "Nghỉ việc nhiều, phải tuyển lại"),
            ("STAFF_ADVICE", "Tư vấn chưa tốt"),
            ("STAFF_PASSIVE", "Không chủ động, phải nhắc từng việc"),
            ("STAFF_CONTROL", "Khó kiểm soát tiền, hàng")),
          scorable=False, meta=when("PAIN_STAFF")),
    ]),
    ("BUSINESS", "Kinh doanh", [
        q("B1", "Anh/chị có biết tháng trước nhà thuốc lãi bao nhiêu không?",
          S("Không — chỉ biết khi tổng kết cuối năm",
            "Đoán theo tiền còn trong két",
            "Tính được nhưng mất công",
            "Biết rõ con số hằng tháng"), impact=1.2),
        q("B2", "Anh/chị có biết nhóm hàng nào lãi nhiều nhất (không phải bán nhiều nhất)?",
          S("Không", "Đoán được", "Biết, nhờ tính tay", "Có số liệu rõ ràng"),
          impact=0.9),
        q("B3", "Lần khuyến mại gần nhất, anh/chị có biết nó lời hay lỗ không?",
          S("Không biết", "Chỉ thấy đông khách hơn",
            "So được số bán trước và sau",
            "Tính được phần lãi thêm sau khi trừ giảm giá",
            UNK("Chưa làm khuyến mại")), impact=0.7),
        q("B4", "Tháng nào bán chậm hẳn, việc đầu tiên anh/chị làm là gì?",
          S("Chờ xem tháng sau",
            "Đoán nguyên nhân, thử giảm giá",
            "Lục sổ xem chậm ở đâu",
            "Xem số theo nhóm hàng, nhóm khách để tìm chỗ chậm"), impact=0.9),
        q("B5", "Anh/chị muốn mỗi ngày nhìn thấy con số nào nhất?",
          N(("NUM_MARGIN", "Lãi trong ngày"),
            ("NUM_STOCK", "Hàng sắp hết, hàng sắp hết hạn"),
            ("NUM_CUSTOMER", "Khách mới, khách quay lại"),
            ("NUM_STAFF", "Kết quả của từng nhân viên")),
          scorable=False, meta=when("PAIN_NUMBERS")),
    ]),
    ("TECH", "Dữ liệu & Công nghệ", [
        q("T1", "Việc bán hàng hiện được ghi lại bằng gì?",
          S("Sổ tay, hoặc không ghi", "Excel",
            "Phần mềm trên một máy tại quầy",
            "Phần mềm, xem được cả trên điện thoại"), impact=0.9),
        q("T2", "Khi không ở nhà thuốc, anh/chị biết hôm nay bán được bao nhiêu bằng cách nào?",
          S("Không biết đến khi về", "Gọi điện hỏi",
            "Nhân viên chụp sổ gửi qua Zalo", "Tự xem trên điện thoại"),
          impact=0.8),
        q("T3", "Thông tin bán hàng, kho và khách hàng đang nằm ở đâu?",
          S("Chưa ghi đủ cả ba", "Mỗi thứ một nơi",
            "Cùng một phần mềm nhưng ít dùng phần kho hoặc khách",
            "Cùng một phần mềm, dùng đủ cả ba"), impact=0.8),
        q("T4", "Việc kết nối liên thông dữ liệu bán thuốc theo yêu cầu của ngành dược đang thế nào?",
          S("Chưa kết nối", "Đang làm, chưa ổn định",
            "Có kết nối nhưng nhập tay nhiều",
            "Kết nối tự động từ phần mềm bán hàng",
            UNK("Không rõ yêu cầu này")),
          meta={"review": "pharmacist"}, impact=1.0),
    ]),
    ("GROWTH", "Phát triển", [
        q("G1", "Anh/chị đã có mục tiêu cụ thể cho 12 tháng tới chưa?",
          S("Chưa nghĩ tới", "Có ý định chung",
            "Có mục tiêu, chưa có kế hoạch",
            "Có mục tiêu, kế hoạch và theo dõi hằng tháng"), impact=0.6),
        q("G2", "Nếu mở thêm một cơ sở, cách làm hiện nay có áp dụng được ngay không?",
          S("Phải làm lại từ đầu",
            "Chỉ áp dụng được một phần, phụ thuộc vào người",
            "Được, nếu anh/chị trực tiếp kèm",
            "Được — đã có quy trình và phần mềm dùng chung",
            UNK("Chưa có ý định mở thêm")), impact=0.6),
        q("G3", "Anh/chị dự định mở thêm trong bao lâu?",
          N(("OPEN_6M", "Trong 6 tháng"), ("OPEN_12M", "6–12 tháng"),
            ("OPEN_LATER", "Sau 1 năm"), ("OPEN_UNSURE", "Chưa chắc")),
          scorable=False, meta=when("PAIN_GROWTH")),
        q("G5", "Nếu chỉ chọn một việc để cải thiện trong 30 ngày tới, anh/chị chọn việc nào?",
          N(("PRI_RETENTION", "Giữ chân khách quen"),
            ("PRI_STOCK", "Giảm hàng tồn, hàng cận hạn"),
            ("PRI_STAFF", "Nhân viên làm đều tay"),
            ("PRI_PROFIT", "Nắm rõ lãi lỗ"),
            ("PRI_COMPLIANCE", "Sổ sách gọn, sẵn sàng khi kiểm tra"),
            ("PRI_DOCTOR", "Kết nối bác sĩ, đơn thuốc online"),
            ("PRI_OTHER", "Việc khác")),
          help="Báo cáo sẽ đặt việc này lên đầu.",
          scorable=False, meta={"leadTag": "priority_need"}),
    ]),
]

# ---------------------------------------------------------------------------
# LUẬT BÁO CÁO
# Mỗi câu tính điểm: khi trả lời mức 1–2 -> một "việc nên làm".
# this_week: làm được ngay, KHÔNG cần phần mềm.
# novixa: chỉ điền khi Novixa thật sự giải quyết được; None = không gắn sản phẩm.
# ---------------------------------------------------------------------------
ACTIONS = {
    "C2": dict(finding="Khách quen quay lại mà nhân viên không biết lần trước họ mua gì, nên khó tư vấn tiếp và khó bán thêm đúng nhu cầu.",
               this_week="Mở một cuốn sổ (hoặc một trang Excel) cho 20 khách quen nhất: tên, số điện thoại, thuốc hay mua. Nhân viên ghi thêm sau mỗi lần bán cho họ.",
               metric="Sau 30 ngày: số khách quen đã có hồ sơ.",
               novixa="Hồ sơ khách gắn với từng đơn bán, tra bằng số điện thoại."),
    "C3": dict(finding="Khách dùng thuốc lâu dài không được nhắc khi sắp hết thuốc. Đây là nhóm khách dễ chuyển sang chỗ khác nhất.",
               this_week="Lập danh sách khách mua thuốc huyết áp, tiểu đường trong tháng, ghi ngày dự kiến hết thuốc. Mỗi sáng xem danh sách và nhắn Zalo cho người sắp hết.",
               metric="Sau 30 ngày: trong số khách được nhắc, bao nhiêu người quay lại mua.",
               novixa="Nhắc lịch mua lại tự động theo đơn trước."),
    "C4": dict(finding="Sau khi bán, nhà thuốc gần như không hỏi thăm lại khách.",
               this_week="Mỗi ngày chọn 3 khách mua thuốc điều trị (kháng sinh, thuốc đau dạ dày…), 2–3 ngày sau nhắn hỏi thăm thuốc có hợp không.",
               metric="Số khách được hỏi thăm mỗi tuần.",
               novixa="Danh sách khách cần hỏi thăm tự sinh theo đơn."),
    "C5": dict(finding="Nhà thuốc chưa biết mỗi tháng có bao nhiêu khách mới, bao nhiêu khách cũ quay lại, nên không biết việc giữ khách đang tốt lên hay xấu đi.",
               this_week="Trong 1 tuần, mỗi đơn bán đánh dấu 'mới' hoặc 'quen' vào sổ. Cuối tuần đếm tổng.",
               metric="Tỷ lệ khách quen trên tổng khách, đo lại sau 30 ngày.",
               novixa="Báo cáo khách mới và khách quay lại theo tháng."),
    "I1": dict(finding="Hàng sắp hết hạn được phát hiện tình cờ hoặc khi đã quá muộn để xử lý.",
               this_week="Thứ Hai hằng tuần, rà các lô hết hạn trong 3 tháng tới. Dán nhãn màu lên hộp và xếp ra trước để bán trước.",
               metric="Tiền hàng phải hủy hoặc trả lại tháng sau so với tháng này.",
               novixa="Cảnh báo cận hạn theo từng lô."),
    "I3": dict(finding="Khách hỏi mua mà nhà thuốc hết hàng khá thường xuyên. Mỗi lần như vậy là một đơn có thể mất, và khách có thể không quay lại.",
               this_week="Để một tờ giấy ở quầy: mỗi lần hết hàng khi khách hỏi, ghi tên thuốc. Cuối tuần nhập bổ sung những mặt hàng bị ghi từ 2 lần trở lên.",
               metric="Số lần hết hàng mỗi tuần.",
               novixa="Mức tồn tối thiểu và gợi ý đặt hàng."),
    "I4": dict(finding="Số lượng đặt hàng dựa vào cảm giác hoặc gợi ý của trình dược viên, nên dễ vừa thiếu hàng bán chạy vừa dư hàng bán chậm.",
               this_week="Chọn 30 mặt hàng bán chạy nhất. Ghi mức tồn tối thiểu cho từng món (bằng khoảng 1 tuần bán), dưới mức đó thì đặt.",
               metric="Số lần hết hàng của 30 mặt hàng này.",
               novixa="Mức tồn tối thiểu và gợi ý đặt hàng."),
    "I5": dict(finding="Đã lâu chưa kiểm kê toàn bộ kho, nên số trên sổ hoặc trên máy có thể lệch xa với hàng thật.",
               this_week="Không cần kiểm kê cả kho cùng lúc. Mỗi tuần kiểm một nhóm hàng (ví dụ tuần này kiểm nhóm kháng sinh), ghi lại phần lệch.",
               metric="Tỷ lệ lệch giữa sổ và hàng thật của từng nhóm.",
               novixa="Kiểm kê theo nhóm hàng trên điện thoại."),
    "I6": dict(finding="Chưa biết hàng nào bán chậm đang giữ vốn.",
               this_week="Đi một vòng kệ, đánh dấu những hộp còn nguyên và có bụi. Lập danh sách 10 mặt hàng đó và ngừng nhập thêm.",
               metric="Tiền vốn nằm trong 10 mặt hàng này, đo lại sau 60 ngày.",
               novixa="Báo cáo hàng chậm bán theo giá trị tồn."),
    "O1": dict(finding="Nhà thuốc phụ thuộc vào anh/chị. Vắng mặt vài ngày là việc bán bị ảnh hưởng.",
               this_week="Viết ra 10 câu nhân viên hay gọi hỏi anh/chị nhất, kèm câu trả lời. Dán ở quầy.",
               metric="Số cuộc gọi hỏi việc trong tuần anh/chị vắng mặt.",
               novixa=None),
    "O2": dict(finding="Nhân viên mới mất nhiều thời gian mới tự đứng quầy được.",
               this_week="Viết danh sách những việc nhân viên mới phải biết trong tuần đầu, và ai kèm từng việc.",
               metric="Số ngày để nhân viên mới tự đứng quầy, ở lần tuyển tiếp theo.",
               novixa=None),
    "O3": dict(finding="Mỗi nhân viên tư vấn một kiểu. Khách có thể nhận lời khuyên khác nhau ở cùng một nhà thuốc.",
               this_week="Chọn 5 bệnh hay gặp nhất. Mỗi bệnh viết 1 trang: hỏi khách gì, khuyên gì, khi nào khuyên đi khám. Cả nhóm cùng thống nhất.",
               metric="Số bệnh đã có hướng dẫn chung.",
               novixa=None),
    "O4": dict(finding="Tiền cuối ngày hay lệch với số đã bán mà không rõ nguyên nhân. Đây có thể là thất thoát.",
               this_week="Đối chiếu tiền với số bán ngay cuối mỗi ca, không để đến cuối ngày. Ghi phần lệch và tên người trực ca.",
               metric="Số ngày lệch tiền trong tháng.",
               novixa="Chốt ca theo từng nhân viên."),
    "O5": dict(finding="Các việc định kỳ như ghi nhiệt độ, độ ẩm và sổ sách chưa được làm đều. Đây thường là phần bị nhắc khi có kiểm tra.",
               this_week="Giao cho một người cụ thể, đặt giờ cố định mỗi ngày, và để sổ ngay cạnh nhiệt kế.",
               metric="Số ngày trong tháng có ghi đủ.",
               novixa=None),
    "B1": dict(finding="Chưa biết rõ mỗi tháng nhà thuốc lãi bao nhiêu.",
               this_week="Cuối tháng này, ghi 4 con số: tiền bán, tiền nhập hàng, tiền thuê, tiền lương và các chi phí khác. Lấy tiền bán trừ ba khoản sau là ra con số lãi đầu tiên.",
               metric="Có con số lãi của 2 tháng liền để so sánh.",
               novixa="Báo cáo lãi gộp theo ngày và theo tháng."),
    "B2": dict(finding="Chưa biết nhóm hàng nào thật sự mang lại lãi. Bán nhiều chưa chắc đã lãi nhiều.",
               this_week="Lấy 20 mặt hàng bán chạy nhất, ghi giá nhập và giá bán của từng món. Xếp theo tiền lãi mỗi hộp.",
               metric="Danh sách 10 mặt hàng lãi nhất để ưu tiên trưng bày và tư vấn.",
               novixa="Lãi gộp theo mặt hàng và nhóm hàng."),
    "B3": dict(finding="Khuyến mại được làm mà chưa biết lời hay lỗ.",
               this_week="Lần khuyến mại tới, ghi số bán của mặt hàng đó 2 tuần trước và 2 tuần trong đợt. Trừ phần đã giảm giá để biết lời hay lỗ.",
               metric="Đợt khuyến mại tới có lãi thêm hay không.",
               novixa=None),
    "B4": dict(finding="Khi bán chậm lại, chưa có cách tìm ra chậm ở đâu.",
               this_week="Chia tiền bán theo 5 nhóm hàng chính và ghi theo tuần. Khi bán chậm thì so nhóm nào giảm.",
               metric="Có số bán theo nhóm hàng của 4 tuần liền.",
               novixa="Số bán theo nhóm hàng, nhóm khách."),
    "T1": dict(finding="Việc bán hàng chưa được ghi lại để tra cứu và tính toán được.",
               this_week="Bắt đầu bằng một file Excel với 4 cột: ngày, mặt hàng, số lượng, tiền. Ghi trong 1 tuần để thấy mình cần gì.",
               metric="Số ngày trong tuần có ghi đủ.",
               novixa="Phần mềm bán hàng cho nhà thuốc."),
    "T2": dict(finding="Không ở nhà thuốc thì không nắm được tình hình bán hàng.",
               this_week="Cuối mỗi ca, nhân viên nhắn Zalo 3 con số: số đơn, tiền thu, những mặt hàng bị hết.",
               metric="Số ngày nhận đủ tin nhắn.",
               novixa="Xem số bán trên điện thoại."),
    "T3": dict(finding="Thông tin bán hàng, kho và khách mỗi thứ một nơi, nên không trả lời được những câu như 'khách nào hay mua hàng sắp hết hạn này'.",
               this_week="Ghi số điện thoại của khách vào cùng chỗ ghi đơn bán, kể cả khi chỉ dùng sổ.",
               metric="Tỷ lệ đơn bán có ghi số điện thoại khách.",
               novixa="Bán hàng, kho và khách hàng trên cùng một phần mềm."),
    "T4": dict(finding="Việc liên thông dữ liệu bán thuốc chưa ổn định hoặc chưa rõ yêu cầu.",
               this_week="Hỏi lại cơ quan quản lý dược địa phương hoặc nhà cung cấp phần mềm xem nhà thuốc đang thiếu bước nào.",
               metric="Nhà thuốc biết rõ mình đã đủ yêu cầu hay chưa.",
               novixa="Kết nối liên thông từ phần mềm bán hàng (cần xác nhận phạm vi hỗ trợ trước khi ghi vào báo cáo)."),
    "G1": dict(finding="Chưa có mục tiêu cụ thể cho năm tới.",
               this_week="Viết 1 mục tiêu đo được cho 90 ngày tới, ví dụ 'giảm hàng hủy còn dưới 1 triệu/tháng'.",
               metric="Có mục tiêu và được xem lại mỗi tháng.",
               novixa=None),
    "G2": dict(finding="Cách làm hiện nay phụ thuộc vào người, nên khó áp dụng sang cơ sở mới.",
               this_week="Ghi lại 3 quy trình quan trọng nhất (nhận hàng, bán hàng, chốt ca) thành từng bước.",
               metric="Số quy trình đã được viết ra.",
               novixa="Phần mềm dùng chung cho nhiều cơ sở."),
}

# Quà dùng ngay (không cần để lại số điện thoại) theo nhóm yếu nhất
GIFTS = {
    "CUSTOMER": "Mẫu sổ khách quen và lịch nhắc thuốc dùng lâu dài (in được, 1 trang)",
    "INVENTORY": "Checklist rà cận hạn hằng tuần và mẫu nhãn màu",
    "OPERATIONS": "Mẫu hướng dẫn tư vấn 5 bệnh hay gặp và checklist tuần đầu cho nhân viên mới",
    "BUSINESS": "Bảng tính lãi tháng đầu tiên (4 con số)",
    "TECH": "File Excel ghi bán hàng 4 cột",
    "GROWTH": "Mẫu viết 3 quy trình cốt lõi",
}

# Ước tính bằng tiền: chỉ tính từ số người làm tự nhập, luôn ghi rõ là ước tính
MIDPOINTS = {
    "P5": {"AOV_LT80": 60000, "AOV_80_150": 115000, "AOV_150_300": 225000, "AOV_GT300": 350000},
    "I2": {"EXP_0": 0, "EXP_LT1M": 500000, "EXP_1_3M": 2000000, "EXP_3_7M": 5000000, "EXP_GT7M": 8000000},
    "I3": {"OPT1": 18, "OPT2": 10, "OPT3": 3.5, "OPT4": 0.5},
}
ESTIMATES = [
    dict(code="EXPIRED_PER_YEAR",
         needs=["I2"],
         formula="I2 * 12",
         show_if="I2 >= 1000000",
         text="Theo số anh/chị nhập, hàng hết hạn phải hủy hoặc trả lại khoảng {value} mỗi năm.",
         caveat="Ước tính từ con số anh/chị nhập, nhân 12 tháng. Chưa trừ phần trả lại được nhà cung cấp."),
    dict(code="STOCKOUT_PER_YEAR",
         needs=["I3", "P5"],
         formula="I3 * 52 * P5",
         show_if="I3 >= 2",
         text="Nếu mỗi lần hết hàng là một đơn không bán được, nhà thuốc đang để lọt khoảng {value} tiền bán mỗi năm.",
         caveat="Đây là mức trên. Có khách sẽ chờ hoặc đổi sang thuốc khác, nên con số thật thấp hơn."),
]

RANKING = {
    "rule": "priority = impact * (4 - score) / 3; câu thuộc nhóm trùng với P3 (nỗi đau) hoặc G5 (ưu tiên) được nhân 1.5",
    "pick": "Lấy 3 việc có priority cao nhất, mỗi nhóm tối đa 1 việc. Chỉ xét câu trả lời mức 1–2.",
    "pain_to_category": {
        "PAIN_CUSTOMER": "CUSTOMER", "PAIN_INVENTORY": "INVENTORY", "PAIN_STAFF": "OPERATIONS",
        "PAIN_NUMBERS": "BUSINESS", "PAIN_COMPLIANCE": "OPERATIONS", "PAIN_GROWTH": "GROWTH"},
    "priority_to_category": {
        "PRI_RETENTION": "CUSTOMER", "PRI_STOCK": "INVENTORY", "PRI_STAFF": "OPERATIONS",
        "PRI_PROFIT": "BUSINESS", "PRI_COMPLIANCE": "OPERATIONS", "PRI_DOCTOR": None, "PRI_OTHER": None},
    "note": "Câu ưu tiên có mã G5 để backend (AssessmentScoringEngine) đọc tag priority_need như V1.",
    "levels": [
        {"min": 80, "label": "Vững"}, {"min": 60, "label": "Khá"},
        {"min": 40, "label": "Cơ bản"}, {"min": 0, "label": "Cần cải thiện"}],
    "unknown_answers": "Phương án có score=null không tính vào điểm trung bình. Nếu một nhóm có từ 50% câu trở lên là null thì hiển thị 'Chưa đủ dữ liệu' thay cho điểm, và đưa vào báo cáo dòng 'Những điều anh/chị chưa đo được'.",
    "benchmark": "Không hiển thị so sánh với nhà thuốc khác cho đến khi mỗi nhóm quy mô (P2) có ít nhất 30 bài hoàn thành. Khi hiển thị thì luôn ghi kèm n.",
}

# Tag cho câu nỗi đau (P3) và ưu tiên (G5). Dùng lại tag V1 khi trùng nghĩa,
# để các nhãn và narrative đang có trong backend vẫn chạy.
OPTION_TAGS = {
    "P3": {"PAIN_CUSTOMER": "pain_retention", "PAIN_INVENTORY": "pain_inventory",
           "PAIN_STAFF": "pain_staff", "PAIN_NUMBERS": "pain_numbers",
           "PAIN_COMPLIANCE": "pain_compliance", "PAIN_GROWTH": "pain_growth"},
    "G5": {"PRI_RETENTION": "need_crm", "PRI_STOCK": "need_inventory", "PRI_STAFF": "need_ops",
           "PRI_PROFIT": "need_profit", "PRI_COMPLIANCE": "need_compliance",
           "PRI_DOCTOR": "need_telemed", "PRI_OTHER": "need_other"},
}

# Tiêu đề ngắn: (tiêu đề phát hiện, tiêu đề việc nên làm)
TITLES = {
    "C2": ("Chưa tra được lịch sử mua của khách quen", "Lập sổ 20 khách quen nhất"),
    "C3": ("Khách dùng thuốc lâu dài chưa được nhắc", "Nhắc khách sắp hết thuốc dùng lâu dài"),
    "C4": ("Chưa hỏi thăm khách sau khi bán", "Hỏi thăm 3 khách mỗi ngày"),
    "C5": ("Chưa đo được khách mới, khách quay lại", "Đếm khách mới và khách quen trong 1 tuần"),
    "I1": ("Hàng cận hạn được phát hiện muộn", "Rà cận hạn mỗi thứ Hai"),
    "I3": ("Hết hàng khi khách hỏi mua", "Ghi lại mỗi lần hết hàng"),
    "I4": ("Đặt hàng theo cảm giác", "Đặt mức tồn tối thiểu cho 30 mặt hàng chạy nhất"),
    "I5": ("Lâu chưa kiểm kê toàn bộ kho", "Kiểm kê xoay vòng từng nhóm hàng"),
    "I6": ("Chưa biết hàng chậm bán đang giữ vốn", "Lập danh sách 10 mặt hàng chậm nhất"),
    "O1": ("Nhà thuốc phụ thuộc vào chủ", "Viết 10 câu nhân viên hay hỏi kèm câu trả lời"),
    "O2": ("Nhân viên mới lâu mới tự đứng quầy", "Viết danh sách việc tuần đầu cho nhân viên mới"),
    "O3": ("Mỗi nhân viên tư vấn một kiểu", "Thống nhất hướng dẫn cho 5 bệnh hay gặp"),
    "O4": ("Tiền cuối ngày hay lệch", "Đối chiếu tiền cuối mỗi ca"),
    "O5": ("Việc định kỳ chưa được ghi đều", "Giao người và giờ cố định cho sổ định kỳ"),
    "B1": ("Chưa biết rõ lãi mỗi tháng", "Tính con số lãi tháng đầu tiên"),
    "B2": ("Chưa biết nhóm hàng nào lãi nhất", "Xếp 20 mặt hàng chạy nhất theo tiền lãi"),
    "B3": ("Khuyến mại chưa biết lời hay lỗ", "Đo lời lỗ cho đợt khuyến mại tới"),
    "B4": ("Chưa có cách tìm chỗ bán chậm", "Ghi số bán theo 5 nhóm hàng mỗi tuần"),
    "T1": ("Việc bán hàng chưa được ghi để tra cứu", "Ghi bán hàng 4 cột trong 1 tuần"),
    "T2": ("Không nắm tình hình khi vắng mặt", "Nhân viên nhắn 3 con số cuối mỗi ca"),
    "T3": ("Thông tin bán hàng, kho, khách nằm rời rạc", "Ghi số điện thoại khách cùng đơn bán"),
    "T4": ("Liên thông dữ liệu chưa ổn định", "Hỏi rõ nhà thuốc còn thiếu bước liên thông nào"),
    "G1": ("Chưa có mục tiêu cho năm tới", "Đặt 1 mục tiêu đo được cho 90 ngày"),
    "G2": ("Cách làm còn phụ thuộc vào người", "Viết ra 3 quy trình cốt lõi"),
}

# productArea cho luật gợi ý: chỉ gán khi phần 'novixa' có nội dung
PRODUCT_AREA = {"CUSTOMER": "crm", "INVENTORY": "inventory", "OPERATIONS": "ops",
                "BUSINESS": "reports", "TECH": "tech", "GROWTH": "multi_branch"}

# Nỗi đau ở P3 gắn với từng câu, không gắn cả nhóm.
PAIN_QUESTIONS = {
    "PAIN_CUSTOMER": ["C2", "C3", "C4", "C5"],
    "PAIN_INVENTORY": ["I1", "I3", "I4", "I5", "I6"],
    "PAIN_STAFF": ["O1", "O2", "O3", "O4"],
    "PAIN_NUMBERS": ["B1", "B2", "B3", "B4"],
    "PAIN_COMPLIANCE": ["O5", "T4"],
    "PAIN_GROWTH": ["G1", "G2", "O1", "O2"],
}

# Nguyên nhân gốc — bản nháp, chờ dược sĩ duyệt. O5 dùng nguyên câu phát hiện, không viết lại.
# (code câu, nhóm, mã nguyên nhân, tiêu đề, câu bằng chứng)
ROOT_CAUSES = [
    ("C3", "CUSTOMER", "RC_V2_C3_REFILL", "Khách thuốc dài ngày không được nhắc", "Câu C3: nhắc khách dùng thuốc lâu dài"),
    ("I1", "INVENTORY", "RC_V2_I1_EXPIRY", "Hàng cận hạn được phát hiện muộn", "Câu I1: theo dõi hàng cận hạn"),
    ("I3", "INVENTORY", "RC_V2_I3_STOCKOUT", "Hết hàng khi khách hỏi mua", "Câu I3: hết hàng lúc khách hỏi"),
    ("B1", "BUSINESS", "RC_V2_B1_PROFIT", "Chưa biết rõ lãi mỗi tháng", "Câu B1: lãi mỗi tháng"),
    ("O1", "OPERATIONS", "RC_V2_O1_OWNER", "Nhà thuốc phụ thuộc vào chủ", "Câu O1: nhà thuốc khi chủ vắng"),
    ("O5", "OPERATIONS", "RC_V2_O5_LOG", "Việc định kỳ chưa được ghi đều", "Câu O5: sổ định kỳ"),
    ("I4", "INVENTORY", "RC_V2_I4_ORDER", "Đặt hàng theo cảm giác", "Câu I4: cách đặt hàng"),
    ("C2", "CUSTOMER", "RC_V2_C2_PROFILE", "Khách quen không có hồ sơ", "Câu C2: hồ sơ khách quen"),
]

# ---------------------------------------------------------------------------
def build():
    cats, qcount = [], 0
    for ci, (ccode, cname, qs) in enumerate(CATEGORIES):
        questions = []
        for qi, Q in enumerate(qs, 1):
            tags = OPTION_TAGS.get(Q["code"], {})
            options = [{"id": uid(Q["code"], o["code"]), "code": o["code"], "label": o["label"],
                        "score": o["score"], "sortOrder": oi,
                        "metadata": {"tag": tags[o["code"]]} if o["code"] in tags else {}}
                       for oi, o in enumerate(Q["options"], 1)]
            meta = dict(Q["meta"])
            if Q["scorable"]:
                meta["impact"] = Q["impact"]
            questions.append({
                "id": uid(Q["code"]), "code": Q["code"], "title": Q["title"],
                "helpText": Q["help"], "questionType": "single_choice",
                "scorable": Q["scorable"], "required": True, "sortOrder": qi,
                "options": options, "metadata": meta or None})
            qcount += 1
        cats.append({"code": ccode, "name": cname, "sortOrder": ci,
                     "weight": 0 if ccode == "PROFILE" else 1,
                     "dimensions": [{"code": f"{ccode}_OVERALL", "name": f"{cname} (tổng)",
                                     "questions": questions}]})
    template = {"id": uid("template"), "code": TEMPLATE_CODE,
                "name": "Soát nhanh nhà thuốc", "version": VERSION, "categories": cats}
    insights = {"templateCode": TEMPLATE_CODE, "version": VERSION, "ranking": RANKING,
                "actions": ACTIONS, "gifts": GIFTS, "midpoints": MIDPOINTS, "estimates": ESTIMATES}
    return template, insights


def validate(t, ins):
    errs = []
    allq = [q for c in t["categories"] for d in c["dimensions"] for q in d["questions"]]
    byc = {q["code"]: q for q in allq}
    ids = [q["id"] for q in allq] + [o["id"] for q in allq for o in q["options"]]
    if len(ids) != len(set(ids)): errs.append("ID trùng")
    if len(byc) != len(allq): errs.append("Mã câu trùng")
    banned = ["Dashboard", "AI ", "loyalty", "doanh thu", ": "]
    for q in allq:
        codes = [o["code"] for o in q["options"]]
        if len(codes) != len(set(codes)): errs.append(f"{q['code']}: mã phương án trùng")
        if q["scorable"]:
            sc = sorted(o["score"] for o in q["options"] if o["score"] is not None)
            if sc != [1, 2, 3, 4]: errs.append(f"{q['code']}: điểm phải đủ 1..4, đang là {sc}")
            if q["code"] not in ins["actions"]: errs.append(f"{q['code']}: thiếu luật báo cáo")
            if q["metadata"] and "showIf" in q["metadata"]:
                errs.append(f"{q['code']}: câu rẽ nhánh không được tính điểm")
        else:
            if any(o["score"] is not None for o in q["options"]): errs.append(f"{q['code']}: câu không tính điểm có score")
        m = q["metadata"] or {}
        if "showIf" in m:
            si = m["showIf"]
            src = byc.get(si["questionCode"])
            if not src or si["optionCode"] not in [o["code"] for o in src["options"]]:
                errs.append(f"{q['code']}: điều kiện rẽ nhánh trỏ sai")
        for o in q["options"]:
            for b in banned:
                if b in o["label"] or b in q["title"]:
                    errs.append(f"{q['code']}: chứa '{b.strip()}' (bị bộ thay chữ của frontend sửa)")
    # số câu hiển thị theo từng nhánh nỗi đau
    pains = [o["code"] for o in byc["P3"]["options"]]
    paths = {}
    for p in pains:
        shown = [q for q in allq if not (q["metadata"] and "showIf" in q["metadata"])
                 or q["metadata"]["showIf"]["optionCode"] == p]
        paths[p] = len(shown)
    return errs, allq, paths


def review_md(t, ins, paths):
    L = [f"# Bộ câu hỏi {t['code']} v{t['version']} — bản duyệt", "",
         "Đánh dấu ✎ ở câu cần sửa chữ. Câu có 🩺 cần dược sĩ xác nhận. ⤷ là câu rẽ nhánh, chỉ hiện khi người làm chọn nỗi đau tương ứng ở câu P3.", "",
         "Số câu người làm thấy: " + ", ".join(f"{k} → {v}" for k, v in paths.items()), ""]
    for c in t["categories"]:
        L += [f"## {c['name']} ({c['code']})", ""]
        for q in c["dimensions"][0]["questions"]:
            m = q["metadata"] or {}
            tags = []
            if not q["scorable"]: tags.append("không tính điểm")
            if "showIf" in m: tags.append(f"⤷ khi {m['showIf']['questionCode']}={m['showIf']['optionCode']}")
            if m.get("review") == "pharmacist": tags.append("🩺")
            if m.get("role") == "input": tags.append("dùng cho ước tính tiền")
            if m.get("role") == "context": tags.append("dùng để diễn giải báo cáo")
            if m.get("leadTag"): tags.append(f"leadTag={m['leadTag']}")
            L.append(f"**{q['code']}. {q['title']}**" + (f"  _({'; '.join(tags)})_" if tags else ""))
            if q["helpText"]: L.append(f"> {q['helpText']}")
            for o in q["options"]:
                s = "—" if o["score"] is None else o["score"]
                L.append(f"- [{s}] {o['label']}")
            a = ins["actions"].get(q["code"])
            if a:
                L += ["", f"  - **Phát hiện (khi trả lời mức 1–2):** {a['finding']}",
                      f"  - **Tuần này làm được:** {a['this_week']}",
                      f"  - **Đo bằng:** {a['metric']}",
                      f"  - **Novixa:** {a['novixa'] or '— không gắn sản phẩm'}"]
            L.append("")
    L += ["## Ước tính bằng tiền", ""]
    for e in ins["estimates"]:
        L += [f"- **{e['code']}** = `{e['formula']}` (hiện khi `{e['show_if']}`)",
              f"  - Câu hiển thị: {e['text']}", f"  - Ghi chú bắt buộc: {e['caveat']}"]
    L += ["", "## Nguyên nhân gốc — BẢN NHÁP, CẦN DƯỢC SĨ DUYỆT", "",
          "Chưa được duyệt. Sinh vào `assessment_root_cause_kb` khi điểm câu ≤ 2. "
          "O5 giữ nguyên câu phát hiện phía trên, không sửa chữ.", ""]
    for code, cat, cause, title, evidence in ROOT_CAUSES:
        L.append(f"- **{code}** ({cat}) `{cause}` — {title}. Hiện khi `response.{code}.score <= 2`. {evidence}.")
        L.append(f"  - {ACTIONS[code]['finding']}")
    return "\n".join(L) + "\n"


if __name__ == "__main__":
    t, ins = build()
    errs, allq, paths = validate(t, ins)
    if errs:
        print("LỖI:"); [print(" -", e) for e in errs]; sys.exit(1)
    json.dump(t, open("PHARMACY_V2.template.json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    json.dump(ins, open("PHARMACY_V2.insights.json", "w", encoding="utf-8"), ensure_ascii=False, indent=2)
    open("PHARMACY_V2.review.md", "w", encoding="utf-8").write(review_md(t, ins, paths))
    scored = [q for q in allq if q["scorable"]]
    print(f"Tổng câu trong ngân hàng: {len(allq)} | tính điểm: {len(scored)}")
    print("Số câu theo nhóm (tính điểm):", Counter(c["code"] for c in t["categories"]
          for q in c["dimensions"][0]["questions"] if q["scorable"]))
    print("Số câu người làm thấy theo nhánh:", paths)
