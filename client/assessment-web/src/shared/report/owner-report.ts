/**
 * Báo cáo cho chủ nhà thuốc (bài PHARMACY_V2), dựng thẳng từ câu trả lời.
 *
 * Nguyên tắc viết:
 * - Nhắc lại đúng câu trả lời của người làm trước khi nhận xét.
 * - Tối đa 2 chỗ mất tiền và 3 việc làm trước; mỗi việc có ai làm, mất bao lâu, sau 30 ngày xem gì.
 * - Lời ở quầy thuốc. Không dùng: doanh nghiệp, AI nhận thấy, nền tảng quản trị, số hóa, pilot, module.
 * - Không có câu áp chung cho mọi nhà thuốc: câu nào cũng gắn với một câu trả lời.
 * - Có phần mềm thì chỉ cách dùng phần mềm đang có trước, rồi mới nói tới Novixa.
 * - Chỉ nhắc Novixa với chức năng đã có trong bảng "Nếu muốn làm tự động" (PHARMACY_V2.insights.json).
 */
import type { AssessmentTemplate } from '@/shared/api/assessment.api';

export type Answers = Record<string, string>; // mã câu -> mã phương án

export type OwnerAction = {
  code: string;
  area: string;
  finding: string;
  said: string;
  why: string;
  title: string;
  steps: string[];
  who: string;
  time: string;
  check: string;
  tool: string | null;
};

export type OwnerLeak = { code: 'EXPIRED' | 'STOCKOUT'; title: string; said: string; body: string; amount: number };

export type OwnerAreaScore = { code: string; name: string; pct: number | null; note: string | null };

export type OwnerReport = {
  headline: string;
  leaks: OwnerLeak[];
  concern: { label: string; text: string } | null;
  actions: OwnerAction[];
  strengths: string[];
  watch: string[];
  areas: OwnerAreaScore[];
  hasSoftware: boolean;
  /** Hai điểm nổi bật cho trang kết quả, trước khi để lại số điện thoại. */
  preview: { title: string; body: string }[];
};

const LEAK_QUESTION: Record<OwnerLeak['code'], string> = { STOCKOUT: 'I3', EXPIRED: 'I1' };

type Opt = 'OPT1' | 'OPT2';

type Ctx = {
  a: Answers;
  solo: boolean;
  hasSoftware: boolean;
};

type QuestionCopy = {
  area: string;
  finding: string;
  problem: string;
  said: Record<Opt, string>;
  why: string | ((o: Opt, c: Ctx) => string);
  title: string | ((c: Ctx) => string);
  steps: string[] | ((o: Opt, c: Ctx) => string[]);
  who: string | ((c: Ctx) => string);
  time: string;
  check: string | ((c: Ctx) => string);
  /** Có phần mềm: dùng phần mềm đang có thế nào. */
  app?: string;
  /** Chưa có phần mềm: chức năng Novixa đã có. */
  novixa?: string;
  strength?: Partial<Record<'OPT3' | 'OPT4', string>>;
  solo?: Partial<Pick<QuestionCopy, 'finding' | 'problem' | 'why' | 'title' | 'steps' | 'check' | 'time' | 'app'>>;
};

const COPY: Record<string, QuestionCopy> = {
  C2: {
    area: 'CUSTOMER',
    finding: 'Chưa tra được khách quen hay mua gì',
    problem: 'chưa tra được khách quen hay mua gì',
    said: {
      OPT1: 'khách quen quay lại thì không biết lần trước họ mua gì, phải hỏi lại khách',
      OPT2: 'khách quen quay lại thì dựa vào nhân viên nhớ mặt, nhớ thuốc',
    },
    why: 'Người nhớ mà nghỉ hoặc bận thì khách phải kể lại từ đầu. Không biết khách hay mua gì thì khó tư vấn tiếp và khó nhắc mua lại.',
    title: 'Ghi lại thuốc hay mua của 20 khách quen nhất',
    steps: [
      'Lấy một cuốn sổ hoặc một trang Excel, ghi 20 khách quen nhất: tên, số điện thoại, thuốc hay mua.',
      'Mỗi lần bán cho các khách này, ghi thêm ngày mua và thuốc đã mua.',
    ],
    who: 'Người đứng quầy',
    time: '10 phút mỗi ngày',
    check: 'Đủ 20 khách có ghi chép, ai đứng quầy cũng tra được.',
    app: 'Mỗi đơn bán nhập số điện thoại khách vào phần mềm, lần sau gõ số điện thoại là thấy khách đã mua gì.',
    novixa: 'hồ sơ khách gắn với từng đơn bán, tra bằng số điện thoại',
    strength: { OPT4: 'Gõ số điện thoại là thấy khách quen đã mua gì', OPT3: 'Tra được khách quen đã mua gì' },
  },
  C3: {
    area: 'CUSTOMER',
    finding: 'Khách dùng thuốc lâu dài chưa được nhắc',
    problem: 'khách dùng thuốc lâu dài chưa được nhắc mua lại',
    said: {
      OPT1: 'khách dùng thuốc huyết áp, tiểu đường sắp hết thuốc thì nhà thuốc không nhắc',
      OPT2: 'khách dùng thuốc huyết áp, tiểu đường sắp hết thuốc thì nhà thuốc chỉ nhắc khi khách nhờ',
    },
    why: 'Đây là nhóm khách mua đều đặn mỗi tháng. Không được nhắc, lúc hết thuốc họ dễ mua luôn ở chỗ gần nhất.',
    title: 'Nhắn Zalo cho khách sắp hết thuốc huyết áp, tiểu đường',
    steps: [
      'Lập danh sách khách mua thuốc huyết áp, tiểu đường, mỡ máu trong tháng: tên, số điện thoại, ngày mua, thuốc dùng được bao nhiêu ngày.',
      'Mỗi sáng xem danh sách, nhắn Zalo cho khách sẽ hết thuốc trong 3 ngày tới.',
    ],
    who: 'Người đứng quầy ca sáng',
    time: '10 phút mỗi sáng',
    check: 'Trong số khách được nhắc, bao nhiêu người quay lại mua.',
    app: 'Lọc trên phần mềm những khách mua thuốc huyết áp, tiểu đường trong 30 ngày qua để lập danh sách cho nhanh.',
    novixa: 'nhắc khách mua lại tự động theo đơn trước',
    strength: {
      OPT4: 'Có danh sách và nhắc đều khách dùng thuốc lâu dài',
      OPT3: 'Có nhắc khách quen dùng thuốc lâu dài',
    },
  },
  C4: {
    area: 'CUSTOMER',
    finding: 'Chưa hỏi thăm khách sau khi bán',
    problem: 'chưa hỏi thăm khách sau khi bán',
    said: {
      OPT1: 'tháng trước nhà thuốc không hỏi thăm lại khách nào sau khi bán',
      OPT2: 'tháng trước nhà thuốc chỉ hỏi thăm lại vài khách sau khi bán',
    },
    why: 'Một tin nhắn hỏi thuốc có đỡ không giúp khách nhớ tới nhà thuốc, và là lý do để họ quay lại lần sau.',
    title: 'Hỏi thăm 3 khách mỗi ngày',
    steps: [
      'Mỗi ngày chọn 3 khách đã mua thuốc cảm, đau dạ dày hoặc thuốc cho trẻ từ 2–3 ngày trước.',
      'Nhắn Zalo hỏi thuốc có đỡ không, có cần dặn thêm gì không.',
    ],
    who: 'Người đứng quầy',
    time: '5 phút mỗi ngày',
    check: 'Khoảng 60 khách đã được hỏi thăm, bao nhiêu người quay lại.',
    app: 'Xem trên phần mềm các đơn 2–3 ngày trước có số điện thoại để chọn khách hỏi thăm.',
    novixa: 'danh sách khách cần hỏi thăm tự lập theo đơn bán',
    strength: { OPT4: 'Hỏi thăm lại nhiều khách sau khi bán', OPT3: 'Có hỏi thăm khách sau khi bán' },
  },
  C5: {
    area: 'CUSTOMER',
    finding: 'Chưa biết khách quen đang tăng hay giảm',
    problem: 'chưa biết khách quen đang tăng hay giảm',
    said: {
      OPT1: 'anh/chị không biết tháng này bao nhiêu khách mới, bao nhiêu khách cũ quay lại',
      OPT2: 'số khách mới, khách cũ quay lại anh/chị chỉ có cảm giác chung',
    },
    why: 'Không có con số thì khách quen bỏ đi cũng không ai biết, cho tới khi tiền bán giảm.',
    title: 'Đếm khách mới và khách quen trong 1 tuần',
    steps: [
      'Để một tờ giấy ở quầy, chia hai cột: khách mới, khách quen. Mỗi khách gạch một vạch.',
      'Cuối tuần cộng lại. Tuần đầu mỗi tháng làm lại để so với tháng trước.',
    ],
    who: 'Người đứng quầy',
    time: 'Vài giây mỗi khách, trong 1 tuần',
    check: 'Có con số khách quen trên tổng số khách, so được tháng sau với tháng này.',
    app: 'Xem trên phần mềm số khách mua lần đầu và số khách mua lại trong tháng (tính theo số điện thoại).',
    novixa: 'báo cáo khách mới và khách quay lại theo tháng',
    strength: { OPT4: 'Biết ngay tháng này bao nhiêu khách mới, khách quay lại' },
  },
  I1: {
    area: 'INVENTORY',
    finding: 'Hàng cận hạn được phát hiện muộn',
    problem: 'hàng cận hạn được phát hiện muộn',
    said: {
      OPT1: 'lần gần nhất anh/chị thấy một lô sắp hết hạn là tình cờ, lúc lấy hàng cho khách',
      OPT2: 'lần gần nhất anh/chị thấy một lô sắp hết hạn là lúc kiểm kê',
    },
    why: (o, c) => {
      const base =
        o === 'OPT1'
          ? 'Thấy tình cờ thì thường đã quá gần ngày hết hạn, khó bán kịp hay trả lại.'
          : 'Kiểm kê vài tháng mới làm một lần, nên nhiều lô tới lúc thấy thì đã quá gần ngày hết hạn.';
      return c.a.I7 === 'NEAR_WAIT'
        ? `${base} Hiện hàng cận hạn đang để tới khi hết hạn rồi hủy, nên mỗi lô thấy muộn là mất trắng.`
        : base;
    },
    title: 'Rà hàng cận hạn mỗi sáng thứ Hai',
    steps: [
      'Sáng thứ Hai, rà các lô hết hạn trong 3 tháng tới. Mỗi tuần rà vài kệ cho nhẹ việc.',
      'Dán nhãn màu lên hộp, xếp ra trước để bán trước. Lô khó bán thì hỏi nhà cung cấp đổi trả sớm.',
    ],
    who: 'Chủ nhà thuốc hoặc người giữ kho',
    time: '15–20 phút mỗi tuần',
    check: 'Tiền hàng phải hủy hoặc trả lại giảm so với tháng này.',
    app: 'Nếu phần mềm có nhập hạn dùng theo lô, mở danh sách lô hết hạn trong 90 ngày thay cho việc rà kệ.',
    novixa: 'báo trước lô sắp hết hạn',
    strength: { OPT4: 'Phần mềm báo trước hàng cận hạn', OPT3: 'Rà hàng cận hạn theo lịch' },
  },
  I3: {
    area: 'INVENTORY',
    finding: 'Hết hàng khi khách hỏi mua',
    problem: 'đang mất đơn vì hết hàng',
    said: {
      OPT1: 'tuần trước có hơn 15 lần khách hỏi mua mà nhà thuốc không có',
      OPT2: 'tuần trước có khoảng 6–15 lần khách hỏi mua mà nhà thuốc không có',
    },
    why: 'Mỗi lần như vậy là một đơn có thể mất. Khách quen gặp vài lần sẽ quen mua chỗ khác.',
    title: 'Ghi lại mỗi lần khách hỏi mà không có thuốc',
    steps: [
      'Để một tờ giấy ở quầy. Khách hỏi mà không có thì ghi tên thuốc.',
      'Cuối tuần, thuốc nào bị ghi từ 2 lần trở lên thì nhập thêm.',
    ],
    who: 'Người đứng quầy',
    time: 'Vài giây mỗi lần, 10 phút cuối tuần',
    check: 'Số lần khách hỏi mà không có thuốc mỗi tuần đã giảm.',
    app: 'Xem phần mềm có báo thuốc sắp hết không. Có thì bật cho các thuốc bán chạy.',
    novixa: 'báo thuốc sắp hết và gợi ý số lượng đặt',
    strength: { OPT4: 'Ít khi hết hàng lúc khách hỏi' },
  },
  I4: {
    area: 'INVENTORY',
    finding: 'Đặt hàng còn theo cảm giác',
    problem: 'nhập hàng còn theo cảm giác',
    said: {
      OPT1: 'anh/chị nhập hàng khi thấy vơi',
      OPT2: 'anh/chị nhập hàng theo kinh nghiệm và gợi ý của trình dược viên',
    },
    why: 'Nhập theo cảm giác thì dễ vừa thiếu thuốc bán chạy vừa dư thuốc bán chậm.',
    title: 'Đặt mức tồn tối thiểu cho 30 thuốc bán chạy nhất',
    steps: [
      'Chọn 30 thuốc bán chạy nhất. Mỗi thuốc ghi một con số "còn dưới mức này thì đặt", khoảng bằng lượng bán 1 tuần.',
      'Mỗi lần đặt hàng, xem 30 thuốc này trước.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 buổi để lập, sau đó 10 phút mỗi lần đặt hàng',
    check: '30 thuốc này không còn bị hết hàng.',
    app: 'Lấy từ báo cáo bán hàng của phần mềm: 30 thuốc bán chạy nhất tháng trước và lượng bán mỗi tuần.',
    novixa: 'mức tồn tối thiểu và gợi ý số lượng đặt',
    strength: { OPT4: 'Có mức tồn tối thiểu cho từng thuốc', OPT3: 'Nhập hàng dựa vào lượng bán tháng trước' },
  },
  I5: {
    area: 'INVENTORY',
    finding: 'Lâu chưa kiểm kê kho',
    problem: 'lâu chưa kiểm kê kho',
    said: {
      OPT1: 'nhà thuốc chưa kiểm kê toàn bộ kho lần nào, hoặc không nhớ lần gần nhất',
      OPT2: 'lần kiểm kê toàn bộ kho gần nhất là hơn 6 tháng trước',
    },
    why: 'Lâu không kiểm thì số trong sổ hoặc phần mềm lệch với hàng trên kệ. Thất thoát hay hàng hỏng đều không ai biết.',
    title: 'Kiểm kê xoay vòng, mỗi tuần một nhóm thuốc',
    steps: [
      'Chia kho thành 4 nhóm, ví dụ: kháng sinh, giảm đau hạ sốt, thực phẩm chức năng, còn lại.',
      'Mỗi tuần đếm một nhóm, so với sổ hoặc phần mềm, ghi lại chỗ lệch.',
    ],
    who: 'Chủ nhà thuốc cùng một người phụ',
    time: '30–45 phút mỗi tuần',
    check: 'Cả kho đã được đếm một lượt, biết chỗ nào lệch.',
    app: 'In danh sách tồn theo từng nhóm từ phần mềm để đếm, rồi nhập lại số lệch.',
    novixa: 'kiểm kê theo nhóm hàng trên điện thoại',
    strength: { OPT4: 'Kiểm kê kho đều đặn' },
  },
  I6: {
    area: 'INVENTORY',
    finding: 'Chưa biết thuốc nào bán chậm đang giữ vốn',
    problem: 'chưa biết thuốc nào bán chậm đang giữ vốn',
    said: {
      OPT1: 'anh/chị chưa kể ngay được 5 thuốc bán chậm nhất đang giữ vốn',
      OPT2: 'anh/chị chỉ đoán được 5 thuốc bán chậm nhất đang giữ vốn',
    },
    why: 'Thuốc nằm lâu trên kệ là tiền nằm im, để lâu lại thành hàng cận hạn.',
    title: 'Lập danh sách 10 thuốc bán chậm nhất',
    steps: [
      'Đi một vòng kệ, đánh dấu những hộp còn nguyên và bám bụi.',
      'Ghi lại 10 thuốc đó. Ngừng nhập thêm, ưu tiên giới thiệu cho khách hoặc trả nhà cung cấp.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 buổi',
    check: 'Tiền hàng nằm trong 10 thuốc này đã giảm.',
    app: 'Xem trên phần mềm những thuốc không bán được trong 60 ngày, sắp theo tiền tồn.',
    novixa: 'báo cáo thuốc chậm bán theo tiền vốn',
    strength: { OPT4: 'Biết ngay thuốc nào bán chậm', OPT3: 'Biết thuốc nào bán chậm' },
  },
  O1: {
    area: 'OPERATIONS',
    finding: 'Nhà thuốc còn phụ thuộc vào anh/chị',
    problem: 'nhà thuốc còn phụ thuộc vào anh/chị',
    said: {
      OPT1: 'nếu anh/chị nghỉ 3 ngày thì nhà thuốc phải đóng cửa hoặc bán rất hạn chế',
      OPT2: 'nếu anh/chị nghỉ 3 ngày thì nhà thuốc vẫn mở nhưng nhân viên gọi hỏi liên tục',
    },
    why: 'Nhà thuốc chạy được hay không đang dựa vào anh/chị, nên anh/chị khó nghỉ, khó đi lấy hàng hay lo việc khác.',
    title: 'Viết sẵn 10 câu nhân viên hay hỏi, kèm câu trả lời',
    steps: [
      'Trong 1 tuần, mỗi lần nhân viên gọi hoặc hỏi anh/chị, ghi lại câu hỏi.',
      'Viết câu trả lời cho 10 câu hay gặp nhất, để ở quầy.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 tuần ghi lại, 1 buổi viết',
    check: 'Số lần nhân viên phải gọi hỏi anh/chị đã giảm.',
    app: 'Cho nhân viên tự tra giá và tồn kho trên phần mềm thay vì hỏi anh/chị.',
    strength: { OPT4: 'Anh/chị vắng vài ngày, nhà thuốc vẫn chạy bình thường', OPT3: 'Anh/chị vắng vài ngày, nhà thuốc vẫn chạy được' },
    solo: {
      finding: 'Anh/chị nghỉ là nhà thuốc phải đóng cửa',
      problem: 'anh/chị nghỉ là nhà thuốc phải đóng cửa',
      why: 'Nhà thuốc chỉ có anh/chị đứng quầy, nên ốm hay có việc là mất cả ngày bán.',
      title: 'Chuẩn bị để có người đứng thay 1–2 ngày',
      steps: [
        'Ghi sẵn: giá 50 thuốc bán chạy, số điện thoại nhà cung cấp, cách mở phần mềm hoặc sổ bán hàng.',
        'Tìm một dược sĩ quen có thể đứng thay khi anh/chị bận, mời đứng cùng 1 buổi cho quen.',
      ],
      check: 'Có người đứng thay được 1 ngày mà không phải gọi anh/chị.',
      time: '2–3 buổi',
      app: 'Viết sẵn vài dòng cách tra giá, tồn kho trên phần mềm để người đứng thay tự làm được.',
    },
  },
  O2: {
    area: 'OPERATIONS',
    finding: 'Nhân viên mới lâu mới quen việc',
    problem: 'nhân viên mới lâu mới quen việc',
    said: {
      OPT1: 'nhân viên mới mất trên 2 tháng mới tự đứng quầy được',
      OPT2: 'nhân viên mới mất 1–2 tháng mới tự đứng quầy được',
    },
    why: 'Người mới lâu quen việc thì anh/chị phải kèm lâu, và trong thời gian đó dễ bán sai.',
    title: 'Viết danh sách việc tuần đầu cho nhân viên mới',
    steps: [
      'Ghi ra những gì người mới cần biết trong tuần đầu: chỗ để thuốc, giá thuốc hay bán, cách ghi đơn, việc đầu ca và cuối ca.',
      'Mỗi ngày người cũ kèm và đánh dấu việc người mới đã làm được.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 buổi để viết',
    check: 'Người mới tiếp theo tự đứng quầy được trong chưa tới 1 tháng.',
    app: 'Cho người mới tập tra giá và chỗ để thuốc trên phần mềm ngay tuần đầu.',
    strength: { OPT4: 'Nhân viên mới quen việc nhanh', OPT3: 'Nhân viên mới quen việc trong vài tuần' },
  },
  O3: {
    area: 'OPERATIONS',
    finding: 'Mỗi nhân viên tư vấn một kiểu',
    problem: 'nhân viên tư vấn chưa giống nhau',
    said: {
      OPT1: 'cùng một bệnh như cảm cúm, mỗi nhân viên tư vấn một kiểu',
      OPT2: 'cùng một bệnh như cảm cúm, nhân viên tư vấn gần giống nhau nhưng là do quen tay',
    },
    why: 'Khách quay lại gặp người khác mà được bán khác thì dễ mất tin. Tư vấn theo trí nhớ cũng dễ sót câu hỏi quan trọng.',
    title: 'Thống nhất cách tư vấn 5 bệnh hay gặp',
    steps: [
      'Chọn 5 bệnh hay gặp nhất, ví dụ: cảm cúm, ho, đau dạ dày, tiêu chảy, dị ứng.',
      'Mỗi bệnh viết 1 trang: câu cần hỏi khách, thuốc nên dùng, khi nào khuyên đi khám. Để ở quầy.',
    ],
    who: 'Dược sĩ phụ trách chuyên môn',
    time: '2–3 buổi',
    check: 'Hỏi thử từng nhân viên cùng một bệnh, mọi người tư vấn giống nhau.',
    strength: { OPT4: 'Có hướng dẫn tư vấn chung cho các bệnh hay gặp' },
  },
  O4: {
    area: 'OPERATIONS',
    finding: 'Tiền cuối ngày hay lệch',
    problem: 'tiền cuối ngày hay lệch',
    said: {
      OPT1: 'cuối ngày tiền thu thường lệch với số đã bán mà không rõ vì sao',
      OPT2: 'cuối ngày tiền thu đôi khi lệch với số đã bán',
    },
    why: 'Lệch ít mỗi ngày cộng lại thành khoản lớn, và khó biết là do quên ghi, thối nhầm hay mất.',
    title: 'Đối chiếu tiền cuối mỗi ca',
    steps: [
      'Cuối mỗi ca, người đứng quầy đếm tiền, so với số đã bán, ghi số lệch và lý do nếu biết.',
      'Cuối tuần anh/chị xem lại: lệch hay rơi vào ca nào, ngày nào.',
    ],
    who: 'Người đứng quầy, anh/chị xem lại cuối tuần',
    time: '5 phút mỗi ca',
    check: 'Số ngày lệch đã giảm, và biết lệch do đâu.',
    app: 'Nếu phần mềm có chốt ca theo từng người, dùng luôn để so tiền với số bán của từng ca.',
    novixa: 'chốt ca theo từng nhân viên',
    strength: { OPT4: 'Tiền cuối ngày khớp, có đối chiếu hằng ngày', OPT3: 'Tiền cuối ngày ít lệch' },
  },
  O5: {
    area: 'OPERATIONS',
    finding: 'Sổ sách định kỳ chưa ghi đều',
    problem: 'sổ sách định kỳ chưa ghi đều',
    said: {
      OPT1: 'các việc định kỳ như ghi nhiệt độ, độ ẩm, vệ sinh, sổ sách chưa làm đều',
      OPT2: 'các việc định kỳ như ghi nhiệt độ, độ ẩm, vệ sinh, sổ sách chỉ làm khi sắp có kiểm tra',
    },
    why: 'Đây là phần hay bị nhắc khi có kiểm tra. Làm dồn trước đợt kiểm tra vừa mệt vừa dễ sót.',
    title: 'Giao người và giờ cố định cho từng sổ',
    steps: (_o, c) => [
      'Viết ra các sổ phải ghi hằng ngày, hằng tuần: nhiệt độ, độ ẩm, vệ sinh, sổ thuốc kê đơn…',
      c.solo
        ? 'Gắn mỗi sổ với một giờ cố định, ví dụ ghi nhiệt độ, độ ẩm lúc mở cửa và trước khi đóng cửa. Dán bảng giờ cạnh nhiệt ẩm kế.'
        : 'Mỗi sổ ghi rõ ai ghi và ghi lúc mấy giờ. Dán bảng này cạnh nhiệt ẩm kế.',
    ],
    who: (c) => (c.solo ? 'Anh/chị' : 'Người được giao từng sổ'),
    solo: { title: 'Đặt giờ cố định cho từng sổ' },
    time: '5–10 phút mỗi ngày',
    check: 'Các sổ không còn ngày bỏ trống.',
    strength: { OPT4: 'Sổ sách định kỳ có người phụ trách, ghi đủ hằng ngày', OPT3: 'Việc định kỳ được làm đều' },
  },
  B1: {
    area: 'BUSINESS',
    finding: 'Chưa biết rõ mỗi tháng lãi bao nhiêu',
    problem: 'chưa biết rõ mỗi tháng lãi bao nhiêu',
    said: {
      OPT1: 'anh/chị chỉ biết nhà thuốc lãi bao nhiêu khi tổng kết cuối năm',
      OPT2: 'anh/chị đoán lãi theo tiền còn trong két',
    },
    why: 'Tiền trong két chưa trừ tiền hàng còn nợ, lương, mặt bằng, hàng hủy, nên dễ tưởng có lãi mà thực ra không.',
    title: 'Tính con số lãi của tháng này',
    steps: [
      'Cuối tháng ghi 4 con số: tiền bán, tiền vốn của hàng đã bán, chi phí (lương, mặt bằng, điện nước), tiền hàng hủy.',
      'Lãi = tiền bán − tiền vốn − chi phí − hàng hủy. Làm 3 tháng liền để so.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1–2 giờ cuối tháng',
    check: 'Có con số lãi của tháng này, tháng sau so được.',
    app: 'Lấy tiền bán và tiền vốn hàng đã bán từ báo cáo lãi của phần mềm, nếu phần mềm có.',
    novixa: 'báo cáo lãi theo ngày và theo tháng',
    strength: { OPT4: 'Biết rõ lãi hằng tháng', OPT3: 'Tính được lãi hằng tháng' },
  },
  B2: {
    area: 'BUSINESS',
    finding: 'Chưa biết nhóm hàng nào lãi nhất',
    problem: 'chưa biết nhóm hàng nào lãi nhất',
    said: {
      OPT1: 'anh/chị chưa biết nhóm hàng nào lãi nhiều nhất',
      OPT2: 'anh/chị chỉ đoán được nhóm hàng nào lãi nhiều nhất',
    },
    why: 'Thuốc bán nhiều chưa chắc lãi nhiều. Không biết nhóm nào lãi thì khó quyết nhập gì, giảm giá gì.',
    title: 'Xếp 20 thuốc bán chạy nhất theo tiền lãi',
    steps: [
      'Lấy 20 thuốc bán chạy nhất tháng trước, ghi giá bán và giá nhập.',
      'Tiền lãi mỗi thuốc = (giá bán − giá nhập) × số đã bán. Xếp từ cao xuống thấp.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 buổi',
    check: 'Biết 5 thuốc mang lại nhiều tiền lãi nhất, và không để chúng hết hàng.',
    app: 'Nếu phần mềm có báo cáo lãi theo mặt hàng, dùng luôn, không cần tính tay.',
    novixa: 'lãi theo từng mặt hàng và nhóm hàng',
    strength: { OPT4: 'Có số liệu nhóm hàng nào lãi nhất', OPT3: 'Biết nhóm hàng nào lãi nhất' },
  },
  B3: {
    area: 'BUSINESS',
    finding: 'Chưa biết khuyến mại lời hay lỗ',
    problem: 'chưa biết khuyến mại lời hay lỗ',
    said: {
      OPT1: 'lần khuyến mại gần nhất anh/chị không biết lời hay lỗ',
      OPT2: 'lần khuyến mại gần nhất anh/chị chỉ thấy đông khách hơn',
    },
    why: 'Đông khách chưa chắc có lãi, vì phần giảm giá có thể ăn hết tiền lời.',
    title: 'Tính lời lỗ cho đợt khuyến mại tới',
    steps: [
      'Trước đợt khuyến mại, ghi số bán 2 tuần trước của các thuốc sẽ giảm giá.',
      'Sau đợt, so số bán và tiền lãi (đã trừ phần giảm) với 2 tuần trước.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1 giờ trước và 1 giờ sau đợt',
    check: 'Biết đợt khuyến mại vừa rồi lời hay lỗ bao nhiêu.',
    app: 'So báo cáo bán hàng trên phần mềm của các thuốc khuyến mại, trước và sau đợt.',
    strength: { OPT4: 'Tính được khuyến mại lời hay lỗ', OPT3: 'So được số bán trước và sau khuyến mại' },
  },
  B4: {
    area: 'BUSINESS',
    finding: 'Tháng bán chậm chưa biết chậm ở đâu',
    problem: 'chưa biết tháng bán chậm là chậm ở đâu',
    said: {
      OPT1: 'tháng nào bán chậm thì anh/chị thường chờ xem tháng sau',
      OPT2: 'tháng nào bán chậm thì anh/chị thường đoán nguyên nhân rồi thử giảm giá',
    },
    why: 'Không biết chậm ở nhóm thuốc nào thì giảm giá dễ mất lãi mà không trúng chỗ.',
    title: 'Ghi tiền bán theo 5 nhóm hàng mỗi tuần',
    steps: [
      'Chia hàng thành 5 nhóm, ví dụ: thuốc kê đơn, thuốc không kê đơn, thực phẩm chức năng, mỹ phẩm, vật tư y tế.',
      'Cuối tuần ghi tiền bán của từng nhóm. Tháng nào chậm thì xem nhóm nào giảm.',
    ],
    who: 'Chủ nhà thuốc',
    time: '15 phút mỗi tuần',
    check: 'Có bảng tiền bán 4 tuần của 5 nhóm hàng.',
    app: 'Xem báo cáo bán hàng theo nhóm hàng trên phần mềm.',
    novixa: 'số bán theo nhóm hàng, nhóm khách',
    strength: { OPT4: 'Tìm được chỗ bán chậm theo nhóm hàng, nhóm khách', OPT3: 'Tìm được chỗ bán chậm khi cần' },
  },
  T1: {
    area: 'TECH',
    finding: 'Việc bán hàng chưa ghi để tra lại được',
    problem: 'việc bán hàng chưa ghi để tra lại được',
    said: {
      OPT1: 'việc bán hàng đang ghi bằng sổ tay, hoặc không ghi',
      OPT2: 'việc bán hàng đang ghi bằng Excel',
    },
    why: (o) =>
      o === 'OPT1'
        ? 'Ghi tay hoặc không ghi thì không tra lại được khách mua gì, thuốc nào bán chạy, lãi bao nhiêu.'
        : 'Excel ghi được nhưng mỗi người ghi một kiểu, khó cộng số và khó tra lại khách mua gì.',
    title: 'Ghi bán hàng theo một mẫu cố định',
    steps: (o) =>
      o === 'OPT1'
        ? [
            'Mỗi đơn bán ghi 4 cột: ngày, tên thuốc, số lượng, tiền. Có số điện thoại khách thì ghi thêm.',
            'Cuối tuần cộng lại để biết thuốc nào bán nhiều nhất.',
          ]
        : [
            'Thống nhất một mẫu Excel: ngày, tên thuốc, số lượng, giá bán, giá nhập, số điện thoại khách.',
            'Mọi người ghi chung một file. Cuối ngày anh/chị xem file đã đủ đơn chưa.',
          ],
    who: 'Người đứng quầy',
    time: 'Vài giây mỗi đơn',
    check: 'Tra lại được đã bán gì, cho ai, trong tuần qua.',
    novixa: 'phần mềm bán hàng cho nhà thuốc',
    strength: { OPT4: 'Bán hàng trên phần mềm, xem được trên điện thoại', OPT3: 'Bán hàng trên phần mềm' },
  },
  T2: {
    area: 'TECH',
    finding: 'Không nắm tình hình khi vắng mặt',
    problem: 'không nắm tình hình khi vắng mặt',
    said: {
      OPT1: 'khi không ở nhà thuốc, anh/chị không biết hôm nay bán được bao nhiêu cho đến khi về',
      OPT2: 'khi không ở nhà thuốc, anh/chị phải gọi điện hỏi mới biết hôm nay bán được bao nhiêu',
    },
    why: 'Không nắm được tình hình khi vắng mặt thì khó yên tâm đi đâu, và có chuyện gì cũng biết muộn.',
    title: 'Nhân viên nhắn 3 con số cuối mỗi ca',
    steps: [
      'Cuối ca, nhân viên nhắn Zalo cho anh/chị: tiền bán, số đơn, thuốc nào hết hàng.',
      'Anh/chị trả lời "đã xem" để nhân viên biết số được đọc.',
    ],
    who: 'Người đứng quầy',
    time: '2 phút mỗi ca',
    check: 'Ngày nào anh/chị cũng biết số bán mà không phải gọi.',
    app: 'Hỏi bên bán phần mềm có bản trên điện thoại không. Có thì cài để anh/chị tự xem số bán.',
    novixa: 'xem số bán trên điện thoại',
    strength: { OPT4: 'Tự xem số bán trên điện thoại khi vắng mặt' },
  },
  T3: {
    area: 'TECH',
    finding: 'Bán hàng, kho, khách mỗi thứ một nơi',
    problem: 'bán hàng, kho và khách mỗi thứ một nơi',
    said: {
      OPT1: 'thông tin bán hàng, kho và khách hàng chưa được ghi đủ cả ba',
      OPT2: 'thông tin bán hàng, kho và khách hàng mỗi thứ nằm một nơi',
    },
    why: 'Mỗi thứ một nơi thì khó trả lời những câu đơn giản như: khách này hay mua gì, thuốc nào sắp hết.',
    title: 'Ghi số điện thoại khách cùng với đơn bán',
    steps: [
      'Từ tuần này, đơn nào của khách quen cũng ghi kèm số điện thoại.',
      'Nhập hàng thì ghi vào cùng chỗ đang ghi bán hàng, không ghi riêng một sổ khác.',
    ],
    who: 'Người đứng quầy',
    time: 'Vài giây mỗi đơn',
    check: 'Tra số điện thoại khách là thấy họ đã mua gì.',
    app: 'Dùng luôn phần kho và phần khách hàng của phần mềm, không ghi thêm sổ hay Excel riêng.',
    novixa: 'bán hàng, kho và khách hàng trên cùng một phần mềm',
    strength: { OPT4: 'Bán hàng, kho, khách hàng cùng một phần mềm' },
  },
  T4: {
    area: 'TECH',
    finding: 'Liên thông dữ liệu bán thuốc chưa ổn định',
    problem: 'liên thông dữ liệu bán thuốc chưa ổn định',
    said: {
      OPT1: 'nhà thuốc chưa kết nối liên thông dữ liệu bán thuốc',
      OPT2: 'nhà thuốc đang làm liên thông dữ liệu bán thuốc nhưng chưa ổn định',
    },
    why: 'Liên thông dữ liệu bán thuốc là yêu cầu của ngành dược, chưa xong thì dễ bị nhắc khi kiểm tra.',
    title: 'Hỏi rõ nhà thuốc còn thiếu bước liên thông nào',
    steps: [
      'Gọi bên bán phần mềm hoặc Sở Y tế, hỏi nhà thuốc đã có tài khoản liên thông chưa và còn thiếu bước nào.',
      'Ghi lại các bước còn thiếu và hẹn ngày làm xong.',
    ],
    who: 'Chủ nhà thuốc',
    time: '1–2 cuộc gọi',
    check: 'Biết rõ còn thiếu bước nào và ngày nào xong.',
    strength: { OPT4: 'Liên thông dữ liệu tự động từ phần mềm' },
  },
  G1: {
    area: 'GROWTH',
    finding: 'Chưa có mục tiêu cho năm tới',
    problem: 'chưa có mục tiêu cho năm tới',
    said: {
      OPT1: 'anh/chị chưa nghĩ tới mục tiêu cho 12 tháng tới',
      OPT2: 'cho 12 tháng tới anh/chị mới có ý định chung',
    },
    why: 'Không có một con số để nhắm thì khó biết năm nay có làm tốt hơn năm ngoái không.',
    title: 'Chọn 1 con số làm mục tiêu cho 90 ngày tới',
    steps: [
      'Chọn một con số, ví dụ: tiền hàng hủy dưới 1 triệu mỗi tháng, hoặc 50 khách dùng thuốc lâu dài được nhắc.',
      'Viết con số đó ở chỗ dễ thấy, cuối mỗi tháng xem lại.',
    ],
    who: 'Chủ nhà thuốc',
    time: '30 phút',
    check: 'Sau 90 ngày, biết đã đạt con số đó hay chưa.',
    strength: { OPT4: 'Có mục tiêu, kế hoạch và theo dõi hằng tháng', OPT3: 'Có mục tiêu cho năm tới' },
  },
  G2: {
    area: 'GROWTH',
    finding: 'Cách làm còn phụ thuộc vào người',
    problem: 'cách làm còn nằm trong đầu từng người',
    said: {
      OPT1: 'nếu mở thêm cơ sở, cách làm hiện nay phải làm lại từ đầu',
      OPT2: 'nếu mở thêm cơ sở, cách làm hiện nay chỉ áp dụng được một phần vì còn phụ thuộc vào người',
    },
    why: 'Cách làm còn nằm trong đầu người cũ thì cơ sở mới dễ lặp lại lỗi cũ, và anh/chị phải chạy qua lại cả hai nơi.',
    title: 'Viết ra 3 quy trình chính',
    steps: [
      'Chọn 3 việc quan trọng nhất: nhập hàng, bán và tư vấn, chốt tiền cuối ngày.',
      'Mỗi việc viết thành các bước ngắn. Nhờ một nhân viên làm thử đúng theo giấy để sửa chỗ còn thiếu.',
    ],
    who: 'Chủ nhà thuốc',
    time: '3 buổi',
    check: 'Một nhân viên làm đúng 3 việc này chỉ cần nhìn giấy, không phải hỏi.',
    app: 'Nếu mở thêm cơ sở, hỏi bên bán phần mềm có dùng chung cho nhiều cơ sở được không.',
    novixa: 'phần mềm dùng chung cho nhiều cơ sở',
    strength: { OPT4: 'Có quy trình và phần mềm dùng chung, mở thêm cơ sở được', OPT3: 'Mở thêm cơ sở được nếu anh/chị trực tiếp kèm' },
  },
};

/** Câu nào gắn với nỗi đau ở P3 (giống build.py PAIN_QUESTIONS). */
const PAIN_QUESTIONS: Record<string, string[]> = {
  PAIN_CUSTOMER: ['C2', 'C3', 'C4', 'C5'],
  PAIN_INVENTORY: ['I1', 'I3', 'I4', 'I5', 'I6'],
  PAIN_STAFF: ['O1', 'O2', 'O3', 'O4'],
  PAIN_NUMBERS: ['B1', 'B2', 'B3', 'B4'],
  PAIN_COMPLIANCE: ['O5', 'T4'],
  PAIN_GROWTH: ['G1', 'G2', 'O1', 'O2'],
};

const PRIORITY_AREA: Record<string, string | null> = {
  PRI_RETENTION: 'CUSTOMER',
  PRI_STOCK: 'INVENTORY',
  PRI_STAFF: 'OPERATIONS',
  PRI_PROFIT: 'BUSINESS',
  PRI_COMPLIANCE: 'OPERATIONS',
  PRI_DOCTOR: null,
  PRI_OTHER: null,
};

const STAFF_ONLY = new Set(['O2', 'O3']);

const AREA_NAMES: Record<string, string> = {
  CUSTOMER: 'Khách hàng',
  INVENTORY: 'Kho và hạn dùng',
  OPERATIONS: 'Vận hành, nhân viên',
  BUSINESS: 'Lãi lỗ',
  TECH: 'Ghi chép, phần mềm',
  GROWTH: 'Sẵn sàng mở rộng',
};

const AOV_MID: Record<string, number> = { AOV_LT80: 60000, AOV_80_150: 115000, AOV_150_300: 225000, AOV_GT300: 350000 };
const AOV_LABEL: Record<string, string> = {
  AOV_LT80: 'dưới 80.000đ',
  AOV_80_150: '80.000–150.000đ',
  AOV_150_300: '150.000–300.000đ',
  AOV_GT300: 'trên 300.000đ',
};
const STOCKOUT_PER_WEEK: Record<string, number> = { OPT1: 18, OPT2: 10, OPT3: 3.5 };
const STOCKOUT_LABEL: Record<string, string> = { OPT1: 'hơn 15 lần', OPT2: 'khoảng 6–15 lần', OPT3: 'khoảng 2–5 lần' };
const EXPIRED_MONTH: Record<string, number> = { EXP_1_3M: 2_000_000, EXP_3_7M: 5_000_000, EXP_GT7M: 8_000_000 };
const EXPIRED_LABEL: Record<string, string> = { EXP_1_3M: '1–3 triệu', EXP_3_7M: '3–7 triệu', EXP_GT7M: 'trên 7 triệu' };

function millions(v: number): number {
  const m = v / 1_000_000;
  return m >= 10 ? Math.round(m / 5) * 5 : Math.round(m);
}

function sentence(s: string): string {
  return s.charAt(0).toUpperCase() + s.slice(1);
}

function pick<T>(v: T | ((...args: never[]) => T), ...args: unknown[]): T {
  return typeof v === 'function' ? (v as (...a: unknown[]) => T)(...args) : v;
}

/** Đổi câu trả lời theo id (từ API) sang theo mã câu / mã phương án. */
export function answersByCode(
  template: AssessmentTemplate,
  responses: Record<string, { optionId?: string | null } | undefined>,
): Answers {
  const out: Answers = {};
  for (const cat of template.categories) {
    for (const dim of cat.dimensions) {
      for (const q of dim.questions) {
        const optionId = responses[q.id]?.optionId;
        if (!optionId) continue;
        const opt = q.options.find((o) => o.id === optionId);
        if (opt) out[q.code] = opt.code;
      }
    }
  }
  return out;
}

function scoreOf(template: AssessmentTemplate, code: string, optionCode: string | undefined): number | null {
  if (!optionCode) return null;
  for (const cat of template.categories)
    for (const dim of cat.dimensions)
      for (const q of dim.questions)
        if (q.code === code) return q.options.find((o) => o.code === optionCode)?.score ?? null;
  return null;
}

function impactOf(template: AssessmentTemplate, code: string): number {
  for (const cat of template.categories)
    for (const dim of cat.dimensions)
      for (const q of dim.questions)
        if (q.code === code) {
          const m = q.metadata as { impact?: number } | null | undefined;
          return typeof m?.impact === 'number' ? m.impact : 0.8;
        }
  return 0.8;
}

function labelOf(template: AssessmentTemplate, code: string, optionCode: string | undefined): string | null {
  if (!optionCode) return null;
  for (const cat of template.categories)
    for (const dim of cat.dimensions)
      for (const q of dim.questions)
        if (q.code === code) return q.options.find((o) => o.code === optionCode)?.label ?? null;
  return null;
}

function buildLeaks(a: Answers): OwnerLeak[] {
  const leaks: OwnerLeak[] = [];
  const exp = EXPIRED_MONTH[a.I2 ?? ''];
  if (exp) {
    const yearly = exp * 12;
    const amount = a.I2 === 'EXP_GT7M' ? 84 : millions(yearly);
    const text = a.I2 === 'EXP_GT7M' ? 'trên 84 triệu' : `khoảng ${amount} triệu`;
    leaks.push({
      code: 'EXPIRED',
      amount,
      title: `Hàng hết hạn: ${text} mỗi năm`,
      said: `Anh/chị cho biết tháng trước hàng hết hạn phải hủy hoặc trả lại khoảng ${EXPIRED_LABEL[a.I2!]}.`,
      body: `Nhân lên 12 tháng là ${text} mỗi năm. Phần trả lại được nhà cung cấp chưa trừ ra, nhưng phần phải hủy là tiền mất hẳn.`,
    });
  }
  const perWeek = STOCKOUT_PER_WEEK[a.I3 ?? ''];
  const aov = AOV_MID[a.P5 ?? ''];
  if (perWeek && aov) {
    const amount = millions(perWeek * 52 * aov);
    leaks.push({
      code: 'STOCKOUT',
      amount,
      title: `Hết hàng: có thể mất tới ${amount} triệu tiền bán mỗi năm`,
      said: `Anh/chị cho biết tuần trước có ${STOCKOUT_LABEL[a.I3!]} khách hỏi mà quầy không có thuốc, mỗi đơn trung bình ${AOV_LABEL[a.P5!]}.`,
      body: `Nếu mỗi lần như vậy khách sang chỗ khác mua, một năm nhà thuốc có thể mất tới khoảng ${amount} triệu tiền bán. Thực tế thấp hơn vì có khách chịu chờ hoặc đổi thuốc khác.`,
    });
  }
  return leaks;
}

const CONCERN: Record<string, (a: Answers) => string | null> = {
  PAIN_CUSTOMER: (a) =>
    ({
      WHY_PRICE:
        'Anh/chị nghĩ khách đi vì giá cao hơn chỗ khác. Khách thường chỉ so giá ở vài chục thuốc quen. Thử so giá 20 thuốc bán chạy nhất với 2 nhà thuốc gần nhất: chênh nhiều thì chỉnh riêng những thuốc đó. Nếu chênh ít, lý do chính thường là khách không được nhớ và không được nhắc, việc này làm được ngay mà không phải giảm giá.',
      WHY_CONVENIENCE:
        'Anh/chị nghĩ khách đi vì chỗ khác tiện đường hơn. Có thể bù bằng cách nhận đặt thuốc qua Zalo, chuẩn bị sẵn để khách ghé lấy nhanh, hoặc giao cho khách quen ở gần.',
      WHY_FORGET:
        'Anh/chị nghĩ khách không quay lại vì không được nhắc nên quên. Đúng vậy: nhắc đúng lúc là cách giữ khách rẻ nhất, nhất là với khách dùng thuốc huyết áp, tiểu đường.',
      WHY_STOCKOUT:
        'Anh/chị nghĩ khách đi vì lúc cần thì nhà thuốc hết hàng. Vậy giữ khách bắt đầu từ việc không để hết các thuốc khách quen hay mua: ghi lại mỗi lần khách hỏi mà không có, cuối tuần nhập bổ sung.',
    })[a.C6 ?? ''] ?? null,
  PAIN_INVENTORY: (a) =>
    ({
      NEAR_WAIT:
        'Hiện hàng cận hạn đang để tới khi hết hạn rồi hủy. Chỉ cần thấy sớm 2–3 tháng là có thể giảm giá bán trước hoặc trả nhà cung cấp, thay vì mất trắng.',
      NEAR_RETURN:
        'Anh/chị đang trả nhà cung cấp khi được. Phần còn lại là thấy hàng cận hạn đủ sớm, trước hạn mà nhà cung cấp còn nhận trả.',
      NEAR_PUSH:
        'Anh/chị đã giảm giá và ưu tiên bán trước hàng cận hạn, đó là cách đúng. Việc còn lại là thấy sớm hơn, và nhập vừa đủ để ít hàng rơi vào cận hạn.',
      NEAR_NONE:
        'Hiện chưa có cách làm thống nhất cho hàng cận hạn. Nên chốt một quy tắc chung: còn 3 tháng thì dán nhãn và bán trước, còn 2 tháng thì hỏi trả nhà cung cấp.',
    })[a.I7 ?? ''] ?? null,
  PAIN_STAFF: (a) =>
    ({
      STAFF_TURNOVER:
        'Nhân viên nghỉ nhiều thì mỗi lần tuyển lại là kèm lại từ đầu. Viết sẵn việc tuần đầu và cách tư vấn các bệnh hay gặp giúp người mới quen việc nhanh, anh/chị đỡ phải kèm.',
      STAFF_ADVICE:
        'Muốn nhân viên tư vấn tốt và giống nhau, cần một tờ hướng dẫn chung cho các bệnh hay gặp, không chỉ dựa vào kinh nghiệm từng người.',
      STAFF_PASSIVE:
        'Nhân viên phải nhắc từng việc thường là vì việc chưa được viết ra. Một bảng việc đầu ca, cuối ca có ghi tên người làm sẽ đỡ phải nhắc.',
      STAFF_CONTROL:
        'Khó kiểm soát tiền và hàng thì bắt đầu từ hai việc: chốt tiền cuối mỗi ca, và mỗi tuần kiểm kê một nhóm thuốc.',
    })[a.O7 ?? ''] ?? null,
  PAIN_NUMBERS: (a) =>
    ({
      NUM_MARGIN:
        'Anh/chị muốn mỗi ngày thấy tiền lãi. Bước đầu là tính được lãi theo tháng bằng 4 con số; quen rồi thì làm theo tuần, theo ngày.',
      NUM_STOCK:
        'Anh/chị muốn mỗi ngày thấy hàng sắp hết và hàng sắp hết hạn. Bước đầu là mức tồn tối thiểu cho 30 thuốc bán chạy và lịch rà cận hạn mỗi tuần.',
      NUM_CUSTOMER:
        'Anh/chị muốn mỗi ngày thấy khách mới, khách quay lại. Bước đầu là ghi số điện thoại khách cùng đơn bán, rồi đếm theo tuần.',
      NUM_STAFF:
        'Anh/chị muốn thấy kết quả của từng nhân viên. Bước đầu là mỗi ca ghi rõ ai đứng quầy và tiền bán của ca đó, cuối tuần so giữa các ca.',
    })[a.B5 ?? ''] ?? null,
  PAIN_COMPLIANCE: (a) => {
    const base =
      ({
        INSP_NONE_YET:
          'Nhà thuốc chưa bị kiểm tra lần nào. Phần hay bị nhắc nhất là sổ nhiệt độ, độ ẩm, sổ thuốc kê đơn và cách xếp thuốc, nên chuẩn bị trước từ những phần này.',
        INSP_RECORDS:
          'Lần trước nhà thuốc bị nhắc về sổ sách, ghi chép. Cách chắc nhất là mỗi sổ có người ghi và giờ ghi cố định, không để dồn tới lúc kiểm tra.',
        INSP_STORAGE:
          'Lần trước nhà thuốc bị nhắc về bảo quản, sắp xếp thuốc. Nên xem lại ba chỗ: nhiệt độ, độ ẩm có ghi đủ 2 lần mỗi ngày không; thuốc có xếp theo nhóm, tránh nắng trực tiếp không; hàng cận hạn và hàng chờ hủy có để riêng không.',
        INSP_STAFF:
          'Lần trước nhà thuốc bị nhắc về hồ sơ nhân sự, bằng cấp. Nên lập một danh sách: mỗi người có bản sao bằng cấp, hợp đồng, giấy khám sức khỏe còn hạn, kèm ngày hết hạn để nhắc.',
        INSP_OK:
          'Lần trước nhà thuốc không bị nhắc gì. Giữ nếp đó bằng cách ghi sổ đều hằng ngày, không làm dồn.',
      })[a.O6 ?? ''] ?? null;
    const t4 =
      a.T4 === 'OPT3'
        ? ' Liên thông dữ liệu bán thuốc đang phải nhập tay nhiều: hỏi bên bán phần mềm xem có gửi dữ liệu tự động được không.'
        : '';
    return base ? base + t4 : t4.trim() || null;
  },
  PAIN_GROWTH: (a) =>
    ({
      OPEN_6M:
        'Anh/chị định mở thêm trong 6 tháng tới. Trước khi mở nên có đủ ba thứ: 3 quy trình chính viết ra giấy, một người làm được việc của anh/chị ở cơ sở cũ, và cách xem số bán của cả hai nơi mà không phải có mặt.',
      OPEN_12M:
        'Anh/chị định mở thêm trong 6–12 tháng. Còn đủ thời gian để mỗi tháng viết ra một việc: nhập hàng, bán và tư vấn, chốt tiền cuối ngày.',
      OPEN_LATER:
        'Anh/chị định mở thêm sau 1 năm. Năm nay là lúc làm cho cơ sở hiện tại chạy được khi anh/chị vắng mặt, vì đó là điều kiện để mở thêm.',
      OPEN_UNSURE:
        'Anh/chị chưa chắc khi nào mở thêm. Dù mở hay không, làm cho nhà thuốc chạy được khi anh/chị vắng mặt vẫn là việc nên làm trước.',
    })[a.G3 ?? ''] ?? null,
};

const STRENGTH_INPUTS: Array<(a: Answers) => string | null> = [
  (a) =>
    a.C1 === 'REG_9_10'
      ? 'Gần như khách nào cũng là khách quen'
      : a.C1 === 'REG_6_8'
        ? '6–8 trong 10 khách là khách quen'
        : null,
  (a) => (a.I2 === 'EXP_0' ? 'Gần như không có hàng hết hạn phải hủy' : null),
  (a) => (a.I7 === 'NEAR_PUSH' ? 'Hàng cận hạn được giảm giá, bán trước' : null),
];

const WATCH: Array<(a: Answers) => string | null> = [
  (a) => (a.I2 === 'UNKNOWN' ? 'Chưa rõ tiền hàng hết hạn mỗi tháng: từ tháng này, mỗi lần hủy hay trả hàng thì ghi lại số tiền.' : null),
  (a) => (a.I3 === 'UNKNOWN' ? 'Chưa để ý mỗi tuần khách hỏi mà hết hàng bao nhiêu lần: thử ghi lại trong 1 tuần.' : null),
  (a) => (a.P5 === 'UNKNOWN' ? 'Chưa rõ mỗi đơn trung bình bao nhiêu tiền: lấy tiền bán 1 ngày chia cho số đơn.' : null),
  (a) => (a.C1 === 'UNKNOWN' ? 'Chưa để ý trong 10 khách có mấy khách quen: thử đếm trong 1 ngày.' : null),
  (a) => (a.C4 === 'UNKNOWN' ? 'Chưa theo dõi việc hỏi thăm khách sau khi bán: ghi lại mỗi lần nhắn hỏi thăm.' : null),
  (a) =>
    a.T4 === 'UNKNOWN'
      ? 'Chưa rõ yêu cầu liên thông dữ liệu bán thuốc: hỏi bên bán phần mềm hoặc Sở Y tế xem nhà thuốc cần làm gì.'
      : null,
];

export function buildOwnerReport(
  template: AssessmentTemplate,
  a: Answers,
  categoryScores: { code: string; scorePct: number; insufficientData?: boolean }[],
  overallPct: number,
): OwnerReport {
  const solo = a.O3 === 'UNKNOWN' || a.O2 === 'UNKNOWN';
  const hasSoftware = a.T1 === 'OPT3' || a.T1 === 'OPT4';
  const ctx: Ctx = { a, solo, hasSoftware };
  const painQs = new Set(PAIN_QUESTIONS[a.P3 ?? ''] ?? []);
  const priorityArea = PRIORITY_AREA[a.G5 ?? ''] ?? null;
  const painLeak = (l: OwnerLeak) => painQs.has(LEAK_QUESTION[l.code]) || (priorityArea === 'INVENTORY');
  const leaks = buildLeaks(a).sort((x, y) => Number(painLeak(y)) - Number(painLeak(x)) || y.amount - x.amount);
  const bigLeak = new Set<string>(leaks.filter((l) => l.amount >= 20).map((l) => (l.code === 'STOCKOUT' ? 'I3' : 'I1')));

  // Xếp việc: mức thiếu × tầm quan trọng, ưu tiên câu gắn với điều anh/chị lo nhất.
  const ranked = Object.entries(COPY)
    .map(([code, copy], order) => {
      const opt = a[code];
      const score = scoreOf(template, code, opt);
      if (score == null || score > 2) return null;
      if (solo && STAFF_ONLY.has(code)) return null;
      let p = (impactOf(template, code) * (4 - score)) / 3;
      if (painQs.has(code)) p *= 1.6;
      if (copy.area === priorityArea) p *= 1.25;
      if (bigLeak.has(code)) p *= 1.3;
      return { code, copy, opt: opt as Opt, p, order };
    })
    .filter((x): x is NonNullable<typeof x> => x !== null)
    .sort((x, y) => y.p - x.p || x.order - y.order);

  const perArea = new Map<string, number>();
  const chosen: typeof ranked = [];
  for (const r of ranked) {
    const n = perArea.get(r.copy.area) ?? 0;
    if (n >= 2) continue;
    chosen.push(r);
    perArea.set(r.copy.area, n + 1);
    if (chosen.length === 3) break;
  }

  const actions: OwnerAction[] = chosen.map(({ code, copy, opt }) => {
    const c = solo && copy.solo ? { ...copy, ...copy.solo } : copy;
    const tool = hasSoftware
      ? (c.app ?? null)
      : c.novixa
        ? `Khi muốn làm tự động: ${c.novixa} (Novixa có sẵn).`
        : null;
    return {
      code,
      area: c.area,
      finding: c.finding,
      said: `Anh/chị trả lời là ${c.said[opt].replace(/^anh\/chị /, '')}.`,
      why: pick(c.why, opt, ctx),
      title: pick(c.title, ctx),
      steps: pick(c.steps, opt, ctx),
      who: pick(c.who, ctx),
      time: c.time,
      check: pick(c.check, ctx),
      tool,
    };
  });

  // Điểm mạnh: câu được 4 điểm trước, rồi 3 điểm, rồi các câu nhập số.
  const strengths: string[] = [];
  for (const want of [4, 3]) {
    for (const [code, copy] of Object.entries(COPY)) {
      const opt = a[code];
      if (scoreOf(template, code, opt) !== want) continue;
      if (solo && STAFF_ONLY.has(code)) continue;
      const s = copy.strength?.[opt as 'OPT3' | 'OPT4'];
      if (s && !strengths.includes(s)) strengths.push(s);
    }
  }
  const fromInputs = STRENGTH_INPUTS.map((f) => f(a)).filter((x): x is string => !!x);
  strengths.unshift(...fromInputs);

  const concernText = CONCERN[a.P3 ?? '']?.(a) ?? null;
  const concernLabel = labelOf(template, 'P3', a.P3);
  const concern = concernText && concernLabel ? { label: concernLabel, text: concernText } : null;

  const watch = WATCH.map((f) => f(a)).filter((x): x is string => !!x).slice(0, 2);

  const areas: OwnerAreaScore[] = ['CUSTOMER', 'INVENTORY', 'OPERATIONS', 'BUSINESS', 'TECH', 'GROWTH'].map((code) => {
    const row = categoryScores.find((c) => c.code === code);
    if (code === 'GROWTH' && a.G2 === 'UNKNOWN')
      return { code, name: AREA_NAMES[code], pct: null, note: 'Chưa áp dụng: anh/chị chưa có ý định mở thêm' };
    if (!row || row.insufficientData)
      return { code, name: AREA_NAMES[code], pct: null, note: 'Chưa đủ câu trả lời để chấm' };
    return { code, name: AREA_NAMES[code], pct: Math.round(row.scorePct), note: null };
  });

  const score = Math.round(overallPct);
  const topLeak = leaks.find(painLeak);
  const problem = topLeak
    ? topLeak.code === 'STOCKOUT'
      ? 'đang mất đơn vì hết hàng'
      : 'đang mất tiền vì hàng hết hạn'
    : (chosen[0] ? (solo && chosen[0].copy.solo?.problem) || chosen[0].copy.problem : null);
  const strong = strengths[0];
  let line: string;
  if (problem && strong) line = `${strong}, nhưng ${problem}.`;
  else if (problem) line = `Nên bắt đầu từ chỗ ${problem}.`;
  else if (strong) line = `${strong}. Nhà thuốc đang làm tốt ở hầu hết các phần.`;
  else line = 'Nhà thuốc đang làm tốt ở hầu hết các phần.';
  const headline = `Nhà thuốc của anh/chị đạt ${score}/100. ${sentence(line)}`;

  // Trang kết quả: chỗ mất tiền gắn với điều anh/chị lo (nếu có), rồi việc quan trọng nhất,
  // không lặp cùng một chuyện; còn chỗ trống thì thêm khoản mất tiền lớn nhất.
  const preview: { title: string; body: string }[] = [];
  const shownLeaks = new Set<string>();
  const leakItem = (l: OwnerLeak) => {
    shownLeaks.add(LEAK_QUESTION[l.code]);
    return { title: l.title, body: `${l.said} ${l.body}` };
  };
  if (topLeak) preview.push(leakItem(topLeak));
  for (const act of actions) {
    if (preview.length >= 2) break;
    if (shownLeaks.has(act.code)) continue;
    preview.push({ title: act.finding, body: `${act.said} ${act.why}` });
  }
  // Ô thứ hai: khoản mất tiền lớn (từ 20 triệu/năm) nếu chưa nói tới, vì con số giúp chủ thấy rõ nhất.
  const restLeak = leaks.find((l) => !shownLeaks.has(LEAK_QUESTION[l.code]) && l.amount >= 20);
  if (restLeak && preview.length === 2 && !topLeak) preview[1] = leakItem(restLeak);
  else if (restLeak && preview.length < 2) preview.push(leakItem(restLeak));

  return {
    headline,
    leaks: leaks.slice(0, 2),
    concern,
    actions,
    strengths: strengths.slice(0, 3),
    watch,
    areas,
    hasSoftware,
    preview,
  };
}
