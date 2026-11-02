import { scoreAuthoredShort, scoreHookLine, shortOnlyOverhead, type ScriptScene } from './novixa-shorts';

/**
 * Kiểm tra kịch bản theo luật kênh trước khi khóa: "không kèo, không gợi ý mã, không quảng cáo sàn,
 * số liệu có nguồn và ngày". Chỉ đọc chữ, không gọi AI. Lỗi (error) là vi phạm luật; cảnh báo (warn) là
 * chỗ nên xem lại. Danh sách từ nằm ở các hằng số bên dưới để sửa khi kênh đổi luật.
 */
export type LintIssue = { level: 'error' | 'warn'; code: string; message: string; /** Câu (từ 0) hoặc -1 nếu về cả kịch bản. */ line: number; scene?: number };

/** Lời hứa lợi nhuận. */
const PROMISE = /(chắc chắn (lãi|thắng|có lời)|đảm bảo (lợi nhuận|lãi|không lỗ)|không (bao giờ )?lỗ\b|bao lãi|làm giàu nhanh|x\d+ tài khoản|lãi \d+%\s*(mỗi|một)\s*(ngày|tuần|tháng))/iu;
/** Kèo, tín hiệu, bảo người xem mua/bán. */
const SIGNAL = /(\bkèo\b|tín hiệu (mua|bán)|(?<!không |chưa |đừng )(nên|hãy|cứ) (mua|bán|vào lệnh|long|short)(?!\p{L})(?! (gì|nào|hay|không))|vào lệnh (ngay|theo (mình|tôi))|copy (trade|lệnh)|theo (mình|tôi) (mua|bán|vào))/iu;
/** Gợi ý mã cụ thể. */
const PICK = /((mua|bán|nắm giữ|gom|xả) (mã|cổ phiếu|coin|token) [A-Z0-9]{2,6}\b|mã [A-Z]{3,4} (sẽ|đang|nên))/u;
/** Tên sàn, nhà môi giới: nhắc để quảng cáo là vi phạm. Nhắc trung tính (cơ quan quản lý, công ty chứng khoán nói chung) không bị bắt. */
const EXCHANGES = /\b(binance|okx|bybit|bitget|kucoin|huobi|htx|coinbase|kraken|mexc|gate\.io|exness|xm\b|icmarkets|ic markets|fbs|octafx|vantage|pocket option|olymp trade|quotex|iq option)\b/iu;
const AD = /(đăng ký (tài khoản )?(tại|trên|qua)|mở tài khoản (tại|trên|qua)|link (đăng ký|mở tài khoản)|mã giới thiệu|nhập mã|ưu đãi (phí|nạp)|khuyến mãi (nạp|giao dịch))/iu;
/** Chữ in hoa 3–4 chữ cái thường là mã cổ phiếu hoặc coin; những từ này là viết tắt đã biết nên bỏ qua. */
const KNOWN_ABBR = new Set(['FCA', 'CNBC', 'USD', 'VND', 'NGV', 'ETF', 'ATM', 'GDP', 'CPI', 'FED', 'SEC', 'IMF', 'WTO', 'UBCK', 'UBCKNN', 'HOSE', 'HNX', 'VAT', 'KYC', 'AML', 'APP', 'API', 'USDT', 'FOMO', 'FUD', 'ROI', 'ROE', 'EPS', 'IPO', 'NHNN', 'BTC', 'ETH']);
const TICKER = /\b[A-Z]{3,4}\b/g;
/** Số liệu cần nguồn: có số kèm đơn vị tiền, phần trăm, mức. */
const FIGURE = /(\d[\d.,]*\s*(%|phần trăm|tỷ|nghìn tỷ|triệu|nghìn|đồng|usd|đô)|(hơn|khoảng|gần|tới|lên đến)\s+(\d+|[a-zà-ỹ]+ (mươi|trăm|nghìn|triệu|tỷ)))/iu;
const HYPOTHETICAL = /(giả sử|ví dụ|minh họa|hình dung|nếu bạn|chẳng hạn)/iu;
const SOURCE = /(nguồn|theo (số liệu|báo cáo|thống kê|công bố|tạp chí|cơ quan|ủy ban|ngân hàng|bộ)|công bố|cơ quan quản lý|báo cáo|thống kê|tổng hợp)/iu;
const DATE = /(\d{1,2}\/\d{1,2}(\/\d{2,4})?|tháng\s+(\d{1,2}|[a-zà-ỹ]+)|năm\s+\d{4}|quý\s+\w+|cuối (tháng|năm|quý)|đầu (tháng|năm|quý)|năm nay|năm ngoái|\b20\d{2}\b)/iu;
const DISCLAIMER = /không phải (là )?lời khuyên đầu tư/iu;
const RISK = /rủi ro (mất|thua lỗ)/iu;

function words(text: string): number {
  return text.trim().split(/\s+/).filter(Boolean).length;
}

/** `shortOnly`: chỉ số các câu đánh dấu "[ngắn]". Có dòng đánh dấu nghĩa là kịch bản viết theo đoạn: mỗi cảnh là một video ngắn tự đứng được. */
export function lintScript(lines: string[], scenes: ScriptScene[] = [], shortOnly: number[] = []): LintIssue[] {
  const issues: LintIssue[] = [];
  const at = (i: number) => scenes.find((scene) => i >= scene.from && i <= scene.to)?.n;
  lines.forEach((line, i) => {
    const add = (level: LintIssue['level'], code: string, message: string) => issues.push({ level, code, message, line: i, scene: at(i) });
    if (PROMISE.test(line)) add('error', 'promise', 'Lời hứa lợi nhuận hoặc không lỗ. Phải bỏ hoặc viết lại.');
    if (SIGNAL.test(line)) add('error', 'signal', 'Có kèo hoặc bảo người xem mua/bán. Kênh không gợi ý.');
    if (PICK.test(line)) add('error', 'pick', 'Có gợi ý mã cụ thể. Kênh không gợi ý mã.');
    if (EXCHANGES.test(line) || AD.test(line)) add('error', 'exchange', 'Có tên sàn hoặc lời mời mở tài khoản. Kênh không quảng cáo sàn.');
    const tickers = (line.match(TICKER) ?? []).filter((token) => !KNOWN_ABBR.has(token));
    if (tickers.length) add('warn', 'ticker', `Chữ in hoa ${tickers.join(', ')} có thể là mã cổ phiếu hoặc coin. Nếu đúng thì bỏ, nếu là viết tắt thì bỏ qua.`);
    if (words(line) > 32) add('warn', 'long', `Câu dài ${words(line)} từ, khó đọc và khó hiển thị phụ đề. Nên tách đôi.`);
  });

  if (!lines.some((line) => DISCLAIMER.test(line))) {
    issues.push({ level: 'error', code: 'disclaimer', message: 'Thiếu câu miễn trừ ("…không phải lời khuyên đầu tư").', line: -1 });
  } else if (!lines.some((line) => RISK.test(line))) {
    issues.push({ level: 'warn', code: 'risk', message: 'Có câu miễn trừ nhưng thiếu "rủi ro mất vốn".', line: -1 });
  }

  // Số liệu có nguồn và ngày: xét theo cảnh (nguồn thường nằm cạnh số liệu trong cùng cảnh).
  const spans = scenes.length ? scenes : [{ n: 0, title: '', from: 0, to: lines.length - 1 }];
  for (const scene of spans) {
    const body = lines.slice(scene.from, scene.to + 1);
    const figureAt = body.findIndex((line) => FIGURE.test(line) && !HYPOTHETICAL.test(line) && !DISCLAIMER.test(line));
    if (figureAt < 0) continue;
    const text = body.join(' ');
    if (!SOURCE.test(text)) issues.push({ level: 'warn', code: 'source', message: 'Có số liệu nhưng chưa nói nguồn trong cảnh này.', line: scene.from + figureAt, scene: scene.n || undefined });
    else if (!DATE.test(text)) issues.push({ level: 'warn', code: 'date', message: 'Có số liệu và nguồn nhưng chưa nói ngày hoặc kỳ số liệu.', line: scene.from + figureAt, scene: scene.n || undefined });
  }

  // Cấu trúc để cắt được video ngắn tốt: mỗi cảnh 20–60 giây, mở có hook, khép có câu chốt.
  for (const scene of scenes) {
    const body = lines.slice(scene.from, scene.to + 1);
    if (!body.length) continue;
    const seconds = Math.round(body.reduce((sum, line) => sum + words(line), 0) / 3.6);
    if (seconds > 60) issues.push({ level: 'warn', code: 'scene-long', message: `Cảnh ${scene.n} dài khoảng ${seconds} giây, nên tách để mỗi cảnh thành một video ngắn.`, line: scene.from, scene: scene.n });
    if (seconds < 12 && scenes.length > 1) issues.push({ level: 'warn', code: 'scene-short', message: `Cảnh ${scene.n} chỉ khoảng ${seconds} giây, khó thành video ngắn riêng.`, line: scene.from, scene: scene.n });
    if (shortOnly.length > 0) {
      // Kịch bản viết theo đoạn: mỗi cảnh phải đạt như một video ngắn thật (câu đầu là hook, đủ dài, có chốt, không rủi ro).
      const own = scoreAuthoredShort(body);
      const hasDisclaimerNearby = lines.some((line) => DISCLAIMER.test(line));
      if (own.seconds < 25 || own.seconds > 55) issues.push({ level: 'warn', code: 'short-length', message: `Cảnh ${scene.n} dài khoảng ${own.seconds} giây. Video ngắn nên 25–55 giây.`, line: scene.from, scene: scene.n });
      if (own.scores.hot < 70) {
        issues.push({ level: 'warn', code: 'short-hot', message: `Cảnh ${scene.n} chấm Hot ${own.scores.hot} (Hook ${own.scores.hook}, Hợp lý ${own.scores.coherence}), dưới 70. ${own.notes[0] ?? ''} Gợi ý: ${hookAdvice(body[0])}`.trim(), line: scene.from, scene: scene.n });
      }
      for (const flag of own.riskFlags) if (hasDisclaimerNearby || !/miễn trừ/i.test(flag)) issues.push({ level: 'error', code: 'short-risk', message: `Cảnh ${scene.n}: ${flag}`, line: scene.from, scene: scene.n });
      continue;
    }
    const hook = scoreHookLine(body[0], 0);
    if (hook < 50 && seconds >= 12) {
      issues.push({ level: 'warn', code: 'hook', message: `Cảnh ${scene.n} mở bằng câu chưa gây tò mò (điểm hook ${hook}). Gợi ý: ${hookAdvice(body[0])}`, line: scene.from, scene: scene.n });
    }
  }
  return issues;
}

/** Gợi ý cách viết lại câu mở cảnh, theo ba kiểu hook đã chạy tốt trên kênh. Chỉ là mẫu, người viết tự điền nội dung thật. */
export function hookAdvice(first: string): string {
  const text = first.trim();
  if (/\d/.test(text)) return 'đưa con số lên đầu câu và nói rõ nó là gì ("20 triệu tiền học của con… bạn có dám đưa vào lệnh?").';
  if (/(không|đừng|chưa)/i.test(text)) return 'mở bằng câu loại trừ ngắn ("Kênh này không dành cho người…", "Đừng làm điều này trước khi…").';
  return 'mở bằng một câu hỏi người xem tự trả lời ("Tiền bạn sắp đưa vào thị trường là tiền gì?") hoặc một câu loại trừ ngắn.';
}

export function lintSummary(issues: LintIssue[]) {
  return { errors: issues.filter((i) => i.level === 'error').length, warns: issues.filter((i) => i.level === 'warn').length };
}

/** Chi phí giọng đọc thêm vì các câu chỉ-ngắn (video dài bỏ chúng nhưng vẫn phải đọc). Dùng để người viết biết trước. */
export function voiceOverhead(lines: string[], shortOnly: number[]) {
  return shortOnlyOverhead(lines, shortOnly);
}
