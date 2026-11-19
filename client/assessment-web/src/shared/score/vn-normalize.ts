/** Chuẩn hóa tiếng Việt thiếu dấu — đồng bộ với KapVietnameseText (server). */
const EXACT: Record<string, string> = {
  'Van hanh': 'Vận hành',
  'Du lieu & Cong nghe': 'Dữ liệu & Công nghệ',
  'Phat trien': 'Phát triển',
  'Khach hang': 'Khách hàng',
  'Du lieu roi rac': 'Dữ liệu rời rạc',
  'diem thap': 'điểm thấp',
  'Diem manh': 'Điểm mạnh',
  'Diem yeu': 'Điểm yếu',
  'Rui ro': 'Rủi ro',
  'Cong nghe': 'Công nghệ',
  'Chua': 'Chưa',
  'Khong': 'Không',
  'Thieu': 'Thiếu',
  'Chuan hoa': 'Chuẩn hóa',
  'Quy trinh': 'Quy trình',
  'Ban hang': 'Bán hàng',
  'Ton kho': 'Tồn kho',
  'Nhap hang': 'Nhập hàng',
  'Kiem ke': 'Kiểm kê',
  'Thu cong': 'Thủ công',
  'Tu dong': 'Tự động',
  'Bao cao': 'Báo cáo',
  'Nhan vien': 'Nhân viên',
  'Quan ly': 'Quản lý',
  'Doanh thu': 'Doanh thu',
  'Loi nhuan': 'Lợi nhuận',
  'Chi phi': 'Chi phí',
  'Dau tu': 'Đầu tư',
  'Trien khai': 'Triển khai',
  'Giai doan': 'Giai đoạn',
  'Ke hoach': 'Kế hoạch',
  'Hanh dong': 'Hành động',
  'Muc tieu': 'Mục tiêu',
  'Ket qua': 'Kết quả',
  'Uu tien': 'Ưu tiên',
  'Hieu qua': 'Hiệu quả',
  'Nang suat': 'Năng suất',
  'Phu hop': 'Phù hợp',
  'Duoc': 'Được',
  'Se': 'Sẽ',
  'Da': 'Đã',
  'Can': 'Cần',
  'Nen': 'Nên',
  'Phai': 'Phải',
  'Tot': 'Tốt',
  'Yeu': 'Yếu',
  'Manh': 'Mạnh',
  'Kem': 'Kém',
  'Cham': 'Chậm',
  'Tang': 'Tăng',
  'Giam': 'Giảm',
  'Han che': 'Hạn chế',
  'Cai thien': 'Cải thiện',
  'Lien he': 'Liên hệ',
  'Tu van': 'Tư vấn',
  'Lien ket': 'Liên kết',
  'Dong bo': 'Đồng bộ',
  'Roi rac': 'Rời rạc',
  'Tap trung': 'Tập trung',
  'Phan quyen': 'Phân quyền',
  'Theo doi': 'Theo dõi',
  'Hang can han': 'Hàng cận hạn',
  'Ho so': 'Hồ sơ',
  'Cam tinh': 'Cảm tính',
  'Dinh ky': 'Định kỳ',
  'Quyet dinh': 'Quyết định',
  'Ap dung': 'Áp dụng',
  'Canh bao': 'Cảnh báo',
  'Nha thuoc': 'Nhà thuốc',
  'Chu nha thuoc': 'Chủ nhà thuốc',
  'Nhom': 'Nhóm',
  'He thong': 'Hệ thống',
  'Tinh hinh': 'Tình hình',
  'Tu xa': 'Từ xa',
  'Thong nhat': 'Thống nhất',
  'Phu thuoc': 'Phụ thuộc',
  'Nhan su': 'Nhân sự',
  'Chu chot': 'Chủ chốt',
  'Ty le': 'Tỷ lệ',
  'Chuyen doi so': 'Chuyển đổi số',
  'San sang': 'Sẵn sàng',
  'Doi thu': 'Đối thủ',
  'Khu vuc': 'Khu vực',
  'Thi truong': 'Thị trường',
  'Doanh nghiep': 'Doanh nghiệp',
  'Truong thanh': 'Trưởng thành',
  'Van hanh (tong)': 'Vận hành (tổng)',
  'Khach hang (tong)': 'Khách hàng (tổng)',
};

const PHRASES: [RegExp, string][] = [
  [/\bNhom Du lieu & Cong nghe diem thap\b/gi, 'Nhóm Dữ liệu & Công nghệ điểm thấp'],
  [/\bNhom Van hanh diem thap\b/gi, 'Nhóm Vận hành điểm thấp'],
  [/\bNhom Khach hang diem thap\b/gi, 'Nhóm Khách hàng điểm thấp'],
  [/\bBan hang, kho va KH khong lien ket\b/gi, 'Bán hàng, kho và khách hàng không liên kết'],
  [/\bQuy trinh ban hang va giao ca chua thong nhat\b/gi, 'Quy trình bán hàng và giao ca chưa thống nhất'],
  [/\bphu thuoc nhan su chu chot\b/gi, 'phụ thuộc nhân sự chủ chốt'],
  [/\bchu nha thuoc kho xem tinh hinh tu xa\b/gi, 'chủ nhà thuốc khó xem tình hình từ xa'],
  [/\bra quyet dinh nhanh\b/gi, 'ra quyết định nhanh'],
  [/\bChua theo doi hang can han\b/gi, 'Chưa theo dõi hàng cận hạn'],
  [/\bThieu ho so khach hang tap trung\b/gi, 'Thiếu hồ sơ khách hàng tập trung'],
  [/\bNhap hang theo cam tinh\b/gi, 'Nhập hàng theo cảm tính'],
  [/\bKiem ke thu cong\b/gi, 'Kiểm kê thủ công'],
  [/\bCong nghe chua dong bo\b/gi, 'Công nghệ chưa đồng bộ'],
  [/\bRui ro du lieu roi rac\b/gi, 'Rủi ro dữ liệu rời rạc'],
  [/\bRui ro ton kho\b/gi, 'Rủi ro tồn kho'],
  [/\bchi phi co hoi\b/gi, 'chi phí cơ hội'],
  [/\bkhong hanh dong\b/gi, 'không hành động'],
  [/\bkhong cai thien\b/gi, 'không cải thiện'],
  [/\bkhong co\b/gi, 'không có'],
  [/\bchua co\b/gi, 'chưa có'],
  [/\bchua duoc\b/gi, 'chưa được'],
  [/\bchua chuan hoa\b/gi, 'chưa chuẩn hóa'],
  [/\bcan uu tien\b/gi, 'cần ưu tiên'],
  [/\bcan cai thien\b/gi, 'cần cải thiện'],
  [/\bcan hanh dong\b/gi, 'cần hành động'],
  [/\btrong (\d+) ngay\b/gi, 'trong $1 ngày'],
  [/\btrong (\d+) thang\b/gi, 'trong $1 tháng'],
  [/\b(\d+) ngay dau\b/gi, '$1 ngày đầu'],
  [/\b(\d+)–(\d+) ngay\b/g, '$1–$2 ngày'],
  [/\b(\d+)–(\d+) thang\b/g, '$1–$2 tháng'],
  [/\b va \b/gi, ' và '],
  [/\bKH\b/g, 'khách hàng'],
  [/\bNCC\b/g, 'nhà cung cấp'],
  [/\bHSD\b/g, 'hạn sử dụng'],
  [/\bFEFO\b/g, 'xuất kho theo hạn dùng (FEFO)'],
  [/\bGPP\b/g, 'Thực hành tốt nhà thuốc (GPP)'],
  [/\bSOP\b/g, 'quy trình chuẩn (SOP)'],
  [/\bCRM\b/g, 'quản trị khách hàng (CRM)'],
  [/\bPOS\b/g, 'bán hàng (POS)'],
  [/\bDashboard\b/gi, 'bảng điều khiển'],
  [/\bHeatmap\b/gi, 'bản đồ nhiệt'],
  [/\bPercentile\b/gi, 'phân vị'],
  [/\bGap Analysis\b/gi, 'phân tích khoảng cách'],
  [/\bExecutive Summary\b/gi, 'tóm tắt điều hành'],
  [/\bROI\b/g, 'lợi ích đầu tư (ROI)'],
  [/\bKPI\b/g, 'chỉ số KPI'],
  [/\bSWOT\b/g, 'SWOT (điểm mạnh/yếu, cơ hội, rủi ro)'],
  [/\bHealth Wallet\b/gi, 'Ví sức khỏe'],
  [/\bAI Copilot\b/gi, 'Trợ lý AI'],
  [/\bloyalty\b/gi, 'khách hàng thân thiết'],
  [/\bExcel\b/g, 'Excel (bảng tính)'],
  [/\bAI\b/g, 'AI (trí tuệ nhân tạo)'],
];

const WORDS: [RegExp, string][] = Object.entries(EXACT)
  .sort((a, b) => b[0].length - a[0].length)
  .map(([k, v]) => [new RegExp(`\\b${k.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}\\b`, 'gi'), v]);

/** Có ký tự tiếng Việt có dấu, tức là câu đã viết đúng và không cần thêm dấu nữa. */
const HAS_DIACRITICS = /[\u00C0-\u024F\u1E00-\u1EFF]/;

/** Giữ chữ thường nếu chữ gốc viết thường ("doanh thu" không thành "Doanh thu"). */
function keepCase(matched: string, replacement: string): string {
  const first = matched.charAt(0);
  return first && first === first.toLowerCase() && first !== first.toUpperCase()
    ? replacement.charAt(0).toLowerCase() + replacement.slice(1)
    : replacement;
}

export function normalizeVietnamese(text: string): string {
  if (!text) return text;
  let t = text.trim();
  if (EXACT[t]) return EXACT[t];
  // Bảng thêm dấu chỉ dành cho nội dung cũ viết không dấu. Áp lên câu đã có dấu sẽ làm hỏng
  // câu (\b của JS không hiểu chữ có dấu) và viết hoa sai giữa câu ("Tăng Doanh thu").
  const alreadyAccented = HAS_DIACRITICS.test(t);
  for (const [re, rep] of PHRASES) t = t.replace(re, rep);
  if (alreadyAccented) return t;
  for (const [re, rep] of WORDS) t = t.replace(re, (m) => keepCase(m, rep));
  return t;
}
