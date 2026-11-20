import type { ConsultationProductSuggestion } from '@/shared/api/pharmacy-consultation.api';

export type CounterGroupStatus = 'co' | 'nhap' | 'het';

export type CounterGroup = {
  id: string;
  name: string;
  examples: string[];
  status: CounterGroupStatus;
  note?: string;
  productHints: string[];
};

export type CounterSymptom = {
  id: string;
  label: string;
  category: string;
  phrases: string[];
  codes: string[];
  groups: string[];
  warn?: string;
  ask: string[];
};

export const COUNTER_GROUPS: Record<string, CounterGroup> = {
  tieuchay: { id: 'tieuchay', name: 'Tiêu hóa · chống tiêu chảy, bù nước', examples: ['Smecta', 'Oresol', 'Enterogermina'], status: 'co', productHints: ['smecta', 'gastropulgite', 'diosmectite', 'oresol', 'enterogermina', 'loperamide'] },
  taobon: { id: 'taobon', name: 'Tiêu hóa · nhuận tràng', examples: ['Duphalac', 'Ovalax'], status: 'co', productHints: ['duphalac', 'lactulose', 'bisacodyl', 'ovalax', 'forlax'] },
  daday: { id: 'daday', name: 'Tiêu hóa · dạ dày, ợ nóng', examples: ['Omeprazole', 'Phosphalugel', 'Gaviscon'], status: 'co', productHints: ['omeprazole', 'phosphalugel', 'gaviscon', 'hantacid', 'antacid'] },
  khotieu: { id: 'khotieu', name: 'Tiêu hóa · đầy hơi, khó tiêu', examples: ['men tiêu hóa', 'simethicone'], status: 'nhap', productHints: ['simethicone'] },
  chongnon: { id: 'chongnon', name: 'Chống nôn, say tàu xe', examples: ['thuốc chống say xe'], status: 'nhap', note: 'Uống trước khi đi xe 30 phút.', productHints: ['motilium', 'domperidon'] },
  diung: { id: 'diung', name: 'Dị ứng', examples: ['Loratadine'], status: 'co', productHints: ['loratadine', 'desloratadine', 'cetirizine', 'clarityne', 'zyrtec'] },
  cam: { id: 'cam', name: 'Cảm / mũi', examples: ['thuốc cảm', 'nước muối, xịt rửa mũi'], status: 'nhap', productHints: ['decolgen'] },
  hodom: { id: 'hodom', name: 'Long đờm, ho', examples: ['ACC', 'Prospan'], status: 'co', productHints: ['acc', 'acetylcysteine', 'prospan'] },
  hong: { id: 'hong', name: 'Họng', examples: ['viên ngậm', 'xịt họng', 'nước súc miệng'], status: 'nhap', productHints: [] },
  hasot: { id: 'hasot', name: 'Hạ sốt', examples: ['Paracetamol'], status: 'nhap', note: 'Không dùng quá liều trên nhãn. Tránh dùng chung nhiều thuốc cùng chứa paracetamol.', productHints: ['paracetamol', 'panadol', 'efferalgan', 'tatanol'] },
  giamdau: { id: 'giamdau', name: 'Giảm đau', examples: ['Paracetamol'], status: 'co', productHints: ['paracetamol', 'panadol', 'efferalgan', 'tatanol'] },
  chongviem: { id: 'chongviem', name: 'Chống viêm', examples: [], status: 'co', note: 'Tránh khi đau dạ dày, có thai.', productHints: ['ibuprofen', 'brufen', 'diclofenac', 'meloxicam', 'chymotrypsin'] },
  hoathuyet: { id: 'hoathuyet', name: 'Hoạt huyết', examples: [], status: 'co', productHints: ['ginkgo', 'piracetam', 'hoạt huyết', 'hoat huyet', 'cerebro'] },
  vitamin: { id: 'vitamin', name: 'Vitamin', examples: [], status: 'co', productHints: ['vitamin', 'berocca'] },
  mat: { id: 'mat', name: 'Mắt', examples: ['nhỏ mắt', 'nước rửa mắt'], status: 'co', productHints: ['nhỏ mắt', 'nho mat', 'eyes drop', 'dưỡng mắt', 'duong mat'] },
  mieng: { id: 'mieng', name: 'Nhiệt miệng', examples: ['gel bôi nhiệt miệng'], status: 'nhap', productHints: [] },
  satrung: { id: 'satrung', name: 'Sát trùng, băng vết thương', examples: ['cồn / povidone', 'băng gạc', 'băng cá nhân'], status: 'nhap', productHints: [] },
  bong: { id: 'bong', name: 'Bỏng nhẹ', examples: ['kem bỏng'], status: 'nhap', productHints: [] },
  contrung: { id: 'contrung', name: 'Côn trùng cắn', examples: ['kem bôi côn trùng cắn'], status: 'nhap', productHints: [] },
  nam: { id: 'nam', name: 'Kháng nấm bôi da', examples: ['kem kháng nấm'], status: 'nhap', productHints: [] },
  giun: { id: 'giun', name: 'Tẩy giun', examples: ['Mebendazole / Albendazole'], status: 'nhap', note: 'Không dùng cho phụ nữ có thai, trẻ dưới 1 tuổi.', productHints: [] },
  tri: { id: 'tri', name: 'Trĩ', examples: ['kem / viên đặt trĩ'], status: 'nhap', productHints: [] },
  ham: { id: 'ham', name: 'Hăm tã', examples: ['kem chống hăm'], status: 'nhap', productHints: [] },
  giairuou: { id: 'giairuou', name: 'Giải rượu', examples: ['thuốc giải rượu', 'Oresol'], status: 'nhap', productHints: ['oresol'] },
  anthan: { id: 'anthan', name: 'An thần', examples: [], status: 'het', note: 'Kho chưa có hàng.', productHints: ['melatonin'] },
};

export const COUNTER_SYMPTOMS: CounterSymptom[] = [
  { id: 'tc', label: 'Tiêu chảy', category: 'Tiêu hóa', phrases: ['tiêu chảy', 'ỉa chảy', 'đi ngoài', 'đi lỏng', 'đau bụng đi ngoài'], codes: ['diarrhea'], groups: ['tieuchay'], ask: ['db', 'nn'], warn: 'Đi ngoài ra máu, sốt cao, mất nước (khát nhiều, tiểu ít), trẻ nhỏ đi ngoài nhiều lần.' },
  { id: 'db', label: 'Đau bụng', category: 'Tiêu hóa', phrases: ['đau bụng'], codes: ['abdominal_pain'], groups: ['daday', 'tieuchay'], ask: ['tc', 'nn', 'dd'], warn: 'Đau dữ dội, đau vùng bụng dưới bên phải, bụng cứng, kèm sốt hoặc nôn nhiều: đi khám ngay.' },
  { id: 'tb', label: 'Táo bón', category: 'Tiêu hóa', phrases: ['táo bón', 'khó đi ngoài', 'bí đại tiện'], codes: ['constipation'], groups: ['taobon'], ask: ['dh'], warn: 'Táo bón kéo dài trên 2 tuần hoặc đi ngoài ra máu.' },
  { id: 'on', label: 'Ợ nóng, trào ngược', category: 'Tiêu hóa', phrases: ['ợ nóng', 'ợ chua', 'trào ngược', 'nóng rát thượng vị'], codes: ['heartburn', 'reflux'], groups: ['daday'], ask: ['dd', 'dh'], warn: 'Nuốt nghẹn, nôn ra máu, phân đen, sụt cân.' },
  { id: 'dd', label: 'Đau dạ dày', category: 'Tiêu hóa', phrases: ['đau dạ dày', 'đau thượng vị', 'viêm dạ dày'], codes: ['heartburn'], groups: ['daday'], ask: ['on'], warn: 'Phân đen, nôn ra máu, đau dữ dội.' },
  { id: 'dh', label: 'Đầy hơi, khó tiêu', category: 'Tiêu hóa', phrases: ['đầy hơi', 'đầy bụng', 'chướng bụng', 'ăn không tiêu'], codes: ['bloating', 'heartburn'], groups: ['khotieu', 'daday'], ask: ['tb', 'on'] },
  { id: 'nn', label: 'Buồn nôn, nôn', category: 'Tiêu hóa', phrases: ['buồn nôn', 'nôn', 'ói', 'nôn ói'], codes: ['nausea', 'vomiting'], groups: ['chongnon', 'tieuchay'], ask: ['tc', 'db'], warn: 'Nôn liên tục không uống được nước, nôn ra máu, kèm đau đầu dữ dội.' },
  { id: 'sx', label: 'Say tàu xe', category: 'Tiêu hóa', phrases: ['say xe', 'say sóng', 'say tàu xe'], codes: [], groups: ['chongnon'], ask: ['nn'] },
  { id: 'tr', label: 'Trĩ', category: 'Tiêu hóa', phrases: ['trĩ', 'búi trĩ', 'đi ngoài ra máu'], codes: ['constipation'], groups: ['tri', 'taobon'], ask: ['tb'], warn: 'Chảy máu nhiều hoặc kéo dài.' },
  { id: 'gi', label: 'Tẩy giun', category: 'Tiêu hóa', phrases: ['tẩy giun', 'giun', 'ngứa hậu môn'], codes: [], groups: ['giun'], ask: [] },
  { id: 'so', label: 'Sốt', category: 'Hô hấp, sốt', phrases: ['sốt', 'nóng sốt', 'sốt cao'], codes: ['fever'], groups: ['hasot'], ask: ['hk', 'hd', 'dau', 'tc'], warn: 'Sốt trên 39°C, sốt quá 3 ngày, trẻ dưới 3 tháng sốt, co giật, cứng gáy, phát ban.' },
  { id: 'hk', label: 'Ho khan', category: 'Hô hấp, sốt', phrases: ['ho khan', 'ho'], codes: ['cough_dry'], groups: ['cam', 'hong'], ask: ['hd', 'sm', 'so'], warn: 'Ho trên 2 tuần, ho ra máu, khó thở.' },
  { id: 'hd', label: 'Ho có đờm', category: 'Hô hấp, sốt', phrases: ['ho đờm', 'ho có đờm', 'đờm'], codes: ['cough_phlegm'], groups: ['hodom'], ask: ['so', 'hk'], warn: 'Đờm xanh hoặc vàng đặc kèm sốt, khó thở, ho kéo dài trên 7–10 ngày.' },
  { id: 'sm', label: 'Sổ mũi, nghẹt mũi', category: 'Hô hấp, sốt', phrases: ['sổ mũi', 'nghẹt mũi', 'ngạt mũi', 'cảm cúm', 'cảm lạnh', 'hắt hơi', 'viêm mũi'], codes: ['runny_nose', 'nasal_congestion'], groups: ['cam', 'diung'], ask: ['hk', 'so'] },
  { id: 'dhong', label: 'Đau họng', category: 'Hô hấp, sốt', phrases: ['đau họng', 'viêm họng', 'rát họng'], codes: ['sore_throat'], groups: ['hong', 'giamdau'], ask: ['so', 'hk'], warn: 'Khó nuốt, khó há miệng, sốt cao kéo dài.' },
  { id: 'ng', label: 'Ngứa, mẩn, mề đay', category: 'Da', phrases: ['ngứa', 'mẩn', 'mề đay', 'dị ứng', 'nổi mẩn', 'mẩn ngứa', 'nổi mề đay'], codes: ['allergy', 'itchy_skin', 'hives'], groups: ['diung'], ask: ['ct'], warn: 'Sưng môi, mặt, khó thở: cấp cứu ngay.' },
  { id: 'ct', label: 'Côn trùng cắn', category: 'Da', phrases: ['côn trùng cắn', 'muỗi đốt', 'kiến cắn', 'ong đốt'], codes: ['insect_bite'], groups: ['contrung', 'diung'], ask: ['ng'], warn: 'Ong đốt nhiều nốt, sưng lan rộng, khó thở.' },
  { id: 'nd', label: 'Nấm da, hắc lào', category: 'Da', phrases: ['nấm da', 'hắc lào', 'nấm kẽ chân', 'lang ben', 'nấm'], codes: [], groups: ['nam'], ask: [] },
  { id: 'tx', label: 'Trầy xước, vết thương nhỏ', category: 'Da', phrases: ['trầy xước', 'đứt tay', 'vết thương', 'xước da'], codes: [], groups: ['satrung'], ask: [], warn: 'Vết thương sâu, chảy máu không cầm, do vật bẩn hoặc rỉ sét: cân nhắc tiêm uốn ván.' },
  { id: 'bn', label: 'Bỏng nhẹ', category: 'Da', phrases: ['bỏng', 'phỏng'], codes: [], groups: ['bong'], ask: [], warn: 'Bỏng rộng hơn lòng bàn tay, bỏng ở mặt hoặc bộ phận sinh dục, bỏng do hóa chất hoặc điện.' },
  { id: 'ht', label: 'Hăm tã', category: 'Da', phrases: ['hăm tã', 'hăm', 'hăm da trẻ'], codes: [], groups: ['ham'], ask: [] },
  { id: 'dau', label: 'Đau đầu', category: 'Đau', phrases: ['đau đầu', 'nhức đầu'], codes: ['headache'], groups: ['giamdau', 'chongviem', 'hoathuyet'], ask: ['cm', 'nn', 'so'], warn: 'Đau đầu dữ dội đột ngột, kèm nôn, sốt, cứng gáy, yếu tay chân, nói khó.' },
  { id: 'rang', label: 'Đau răng', category: 'Đau', phrases: ['đau răng', 'nhức răng'], codes: ['toothache'], groups: ['giamdau'], ask: [], warn: 'Sưng má, sốt: cần đi nha khoa.' },
  { id: 'lk', label: 'Đau lưng, đau khớp', category: 'Đau', phrases: ['đau lưng', 'đau khớp', 'đau cơ', 'nhức mỏi', 'đau vai gáy'], codes: ['back_pain', 'joint_pain'], groups: ['giamdau', 'chongviem'], ask: [], warn: 'Sưng nóng đỏ khớp, đau lan xuống chân kèm tê yếu.' },
  { id: 'kinh', label: 'Đau bụng kinh', category: 'Đau', phrases: ['đau bụng kinh', 'hành kinh', 'đau kinh'], codes: [], groups: ['giamdau'], ask: [], warn: 'Đau dữ dội bất thường hoặc chậm kinh kèm đau bụng.' },
  { id: 'matdo', label: 'Đau mắt đỏ', category: 'Khác', phrases: ['đau mắt đỏ', 'mắt đỏ', 'viêm kết mạc', 'ngứa mắt'], codes: ['red_eye'], groups: ['mat'], ask: [], warn: 'Đau nhức mắt nhiều, nhìn mờ, sợ ánh sáng.' },
  { id: 'nm', label: 'Nhiệt miệng', category: 'Khác', phrases: ['nhiệt miệng', 'loét miệng', 'lở miệng'], codes: ['mouth_ulcer'], groups: ['mieng', 'vitamin'], ask: [], warn: 'Vết loét quá 2 tuần không lành.' },
  { id: 'cm', label: 'Chóng mặt', category: 'Khác', phrases: ['chóng mặt', 'hoa mắt', 'choáng'], codes: ['dizziness'], groups: ['hoathuyet'], ask: ['dau'], warn: 'Nên đo huyết áp, đường huyết. Đi khám nếu kèm ngất, nói khó, yếu nửa người.' },
  { id: 'met', label: 'Mệt mỏi', category: 'Khác', phrases: ['mệt mỏi', 'mệt', 'uể oải', 'suy nhược'], codes: ['fatigue'], groups: ['vitamin'], ask: ['mn', 'cm'], warn: 'Mệt kéo dài kèm sụt cân, da xanh: nên đi khám.' },
  { id: 'mn', label: 'Mất ngủ', category: 'Khác', phrases: ['mất ngủ', 'khó ngủ', 'ngủ không ngon'], codes: ['insomnia'], groups: ['anthan'], ask: ['met'] },
  { id: 'sr', label: 'Say rượu', category: 'Khác', phrases: ['say rượu', 'nôn nao', 'giải rượu'], codes: [], groups: ['giairuou'], ask: [], warn: 'Lơ mơ, khó đánh thức, nôn liên tục: đưa đi viện.' },
];

const COMBOS: { need: string[]; text: string }[] = [
  { need: ['so', 'tc'], text: 'Sốt kèm tiêu chảy dễ mất nước. Ưu tiên bù nước bằng Oresol. Đi khám nếu sốt cao hoặc đi ngoài ra máu.' },
  { need: ['so', 'db'], text: 'Sốt kèm đau bụng có thể là nhiễm trùng hoặc viêm ruột thừa. Nên đi khám.' },
  { need: ['so', 'hd'], text: 'Sốt kèm ho đờm: nếu sốt quá 3 ngày hoặc khó thở cần đi khám. Kháng sinh chỉ dùng khi có đơn.' },
  { need: ['dau', 'cm'], text: 'Đau đầu kèm chóng mặt: nên đo huyết áp trước khi bán thuốc.' },
  { need: ['dau', 'nn'], text: 'Đau đầu kèm nôn: nếu đau dữ dội hoặc đột ngột, đi khám ngay.' },
  { need: ['so', 'dau'], text: 'Sốt kèm đau đầu: nếu cứng gáy, phát ban hoặc lơ mơ, đi cấp cứu.' },
];

export function foldCounterText(value: string) {
  return value.toLowerCase().normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd');
}

function tokensOf(value: string) {
  return foldCounterText(value).split(/[^a-z0-9]+/).filter(Boolean);
}

export function matchCounterSheet(text: string) {
  const tokens = tokensOf(text);
  if (tokens.length === 0) return [];
  const hits: { id: string; start: number; length: number }[] = [];
  for (const item of COUNTER_SYMPTOMS) {
    for (const phrase of item.phrases) {
      const phraseTokens = tokensOf(phrase);
      if (phraseTokens.length === 0 || phraseTokens.length > tokens.length) continue;
      for (let start = 0; start <= tokens.length - phraseTokens.length; start += 1) {
        const matches = phraseTokens.every((token, offset) => tokens[start + offset] === token);
        if (matches) hits.push({ id: item.id, start, length: phraseTokens.length });
      }
    }
  }
  return [...new Set(hits
    .filter((hit) => !hits.some((other) => other.length > hit.length && other.start <= hit.start && other.start + other.length >= hit.start + hit.length))
    .map((hit) => hit.id))];
}

export function counterSymptom(id: string) {
  return COUNTER_SYMPTOMS.find((item) => item.id === id);
}

export function codesForSheet(ids: string[]) {
  return [...new Set(ids.flatMap((id) => counterSymptom(id)?.codes ?? []))];
}

export function counterGroupsFor(ids: string[]) {
  const idsByGroup = new Map<string, string[]>();
  for (const id of ids) {
    const item = counterSymptom(id);
    if (!item) continue;
    for (const groupId of item.groups) {
      const list = idsByGroup.get(groupId) ?? [];
      list.push(item.label);
      idsByGroup.set(groupId, list);
    }
  }
  return [...idsByGroup.entries()]
    .map(([groupId, labels]) => ({ group: COUNTER_GROUPS[groupId], labels }));
}

export function counterNotices(ids: string[]) {
  const notices: { tone: 'danger' | 'warning'; text: string }[] = [];
  for (const combo of COMBOS) {
    if (combo.need.every((id) => ids.includes(id))) notices.push({ tone: 'warning', text: combo.text });
  }
  const groups = new Set(ids.flatMap((id) => counterSymptom(id)?.groups ?? []));
  if (groups.has('giamdau') && groups.has('hasot')) {
    notices.push({ tone: 'warning', text: 'Giảm đau và hạ sốt đều có thể là Paracetamol. Chỉ dùng một loại, không cộng dồn liều.' });
  }
  if (groups.has('chongviem') && groups.has('daday')) {
    notices.push({ tone: 'warning', text: 'Đang đau dạ dày thì tránh thuốc chống viêm. Ưu tiên Paracetamol.' });
  }
  for (const id of ids) {
    const item = counterSymptom(id);
    if (item?.warn) notices.push({ tone: 'danger', text: `${item.label}, đi khám khi: ${item.warn}` });
  }
  if (ids.length >= 4) {
    notices.unshift({ tone: 'warning', text: 'Nhiều triệu chứng cùng lúc. Nên hỏi kỹ hoặc khuyên đi khám thay vì bán nhiều thuốc.' });
  }
  return notices;
}

export function counterAsk(ids: string[]) {
  const selected = new Set(ids);
  const asked: string[] = [];
  for (const id of ids) {
    for (const next of counterSymptom(id)?.ask ?? []) {
      if (!selected.has(next) && !asked.includes(next)) asked.push(next);
    }
  }
  return asked.slice(0, 6);
}

export function productsForCounterGroup(group: CounterGroup, suggestions: ConsultationProductSuggestion[]) {
  if (group.productHints.length === 0) return [];
  return suggestions.filter((item) => {
    const text = `${item.productName} ${item.genericName ?? ''}`.toLowerCase();
    if ((group.id === 'hasot' || group.id === 'giamdau') && text.includes('decolgen')) return false;
    return group.productHints.some((hint) => text.includes(hint));
  });
}
