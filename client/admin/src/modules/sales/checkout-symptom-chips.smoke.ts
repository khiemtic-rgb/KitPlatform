import { buildCheckoutSymptomChips } from './checkout-symptom-chips';
import type { ConsultationSymptomCatalog } from '@/shared/api/pharmacy-consultation.api';

function assert(cond: unknown, msg: string): asserts cond {
  if (!cond) throw new Error(msg);
}

const catalog: ConsultationSymptomCatalog = {
  groups: [],
  flat: [
    { code: 'cough', label: 'Ho' },
    { code: 'fever', label: 'Sốt' },
    { code: 'headache', label: 'Đau đầu' },
    { code: 'sore_throat', label: 'Đau họng' },
    { code: 'runny_nose', label: 'Sổ mũi' },
    { code: 'diarrhea', label: 'Tiêu chảy' },
    { code: 'heartburn', label: 'Ợ nóng' },
    { code: 'body_ache', label: 'Đau mình' },
  ],
  aliasesByCode: {
    cough: ['ho', 'terpin'],
    fever: ['sot', 'paracetamol'],
  },
};

const chips = buildCheckoutSymptomChips(catalog, [{ productName: 'Terpin codein' }], 5);
assert(chips[0]?.code === 'cough', `expected cough first, got ${chips[0]?.code}`);
assert(chips.length === 5, `expected 5 chips, got ${chips.length}`);

const empty = buildCheckoutSymptomChips(null, [{ productName: 'X' }]);
assert(empty.length === 0, 'null catalog should yield no chips');

console.log('checkout-symptom-chips.smoke: ok');
