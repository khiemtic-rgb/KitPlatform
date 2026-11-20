/** Video Studio visual-intelligence wiring. No provider calls. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  VISUAL_PROVIDER_HANDOFF,
  afterVisualSearch,
  buildShotVisualDemand,
  interpretVisualResolve,
  normalizeFilmBrand,
} from './visual-studio-still';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const api = read('../../shared/api/content.api.ts');
const execBlock = series.slice(series.indexOf('const executeProductionOsAction'), series.indexOf('const rememberLineAudio'));
const visualFn = series.slice(series.indexOf('const applyVisualCreate'), series.indexOf('const hydrateVisualShot'));
const hydrate = series.slice(series.indexOf('const hydrateVisualShot'), series.indexOf('const executeProductionOsAction'));

ok(normalizeFilmBrand('Famixa') === 'famixa', 'film brand code is kept');
ok(normalizeFilmBrand('Hành trình Trade') === 'trade', 'trade display name maps to the trade profile');
ok(normalizeFilmBrand('') === '', 'missing brand is not replaced with famixa');
ok(normalizeFilmBrand('kittech') === 'kittech', 'an unknown brand is passed through');

const consult = buildShotVisualDemand({
  filmId: 'film-nx',
  brandCode: 'novixa',
  shot: {
    id: 'NX-01',
    location: 'Quầy thuốc',
    visual: 'Tư vấn thuốc tại quầy',
    story: 'Dược sĩ nói thuốc này uống buổi sáng.',
    characterIds: ['CHAR-001'],
  },
});
ok(consult.brandCode === 'novixa' && consult.subjectKey === 'pharmacy-consult', 'novixa counter demand matches the shared still');
ok(consult.characterId === 'CHAR-001', 'a different character stays a reference and is not rewritten as Lan');
ok(!consult.narration.includes('quầy') || consult.prop === '', 'narration is not copied onto a prop');
ok(consult.narration.includes('buổi sáng'), 'spoken copy stays on the narration field');
const labeled = buildShotVisualDemand({
  filmId: 'film-nx',
  brandCode: 'novixa',
  shot: {
    id: 'NX-03',
    location: 'Quầy thuốc',
    visual: 'Hộp «Thuốc 5mg» trên quầy',
    story: 'Dược sĩ nói thuốc này uống buổi sáng.',
  },
});
ok(labeled.requiredLabels?.join('|') === 'Thuốc 5mg', 'a short quoted label is sent as a controlled caption');
ok(!labeled.requiredLabels?.includes(labeled.narration), 'narration is not sent as a caption');

const shoes = buildShotVisualDemand({
  filmId: 'film-fx',
  brandCode: 'famixa',
  shot: { id: 'FX-SHOES', location: 'Hiên mưa', visual: 'Buộc dây giày', story: 'Lan kể chuyện mưa.' },
});
ok(shoes.brandCode === 'famixa' && shoes.subjectKey !== 'pharmacy-consult' && shoes.subjectKey !== 'family-breakfast', 'famixa shoes are not a pharmacy or breakfast still');

const point = buildShotVisualDemand({
  filmId: 'film-fx',
  brandCode: 'famixa',
  shot: { id: 'FX-POINT', location: 'Phòng khách', visual: 'Lan chỉ ra cửa', story: '' },
});
ok(point.pose === 'point', 'a pointing action becomes the point pose');
const coat = buildShotVisualDemand({
  filmId: 'film-nx',
  brandCode: 'novixa',
  shot: { id: 'NX-COAT', location: 'Kho', visual: 'Áo blouse trắng', story: '' },
});
ok(coat.tokens?.includes('garment:coat') === true, 'a blouse note selects the coat garment');
ok(coat.pose === 'stand', 'a wardrobe note does not invent a pose');

const unfit = interpretVisualResolve({
  decision: 'NO_FIT',
  assetId: null,
  hasImage: false,
  paidProviderCalls: 0,
  reason: 'Không đủ đúng.',
  candidates: [{ assetId: 'nx-counter-consult', eligible: true, reasonCode: 'FIT' }],
}, 'create');
ok(unfit.phase === 'missing' && !unfit.assetId && unfit.offerPaid !== true, 'NO_FIT does not select a candidate');
ok(afterVisualSearch({
  decision: 'NO_FIT',
  paidProviderCalls: 0,
  candidates: [{ assetId: 'fx-breakfast', eligible: true }],
}) === 'create', 'NO_FIT continues into a new still, not the candidate');

const reuse = interpretVisualResolve({
  decision: 'REUSE',
  assetId: 'nx-counter-consult',
  hasImage: true,
  paidProviderCalls: 0,
  reasonCodes: ['REUSE'],
}, 'search');
ok(afterVisualSearch({ decision: 'REUSE', assetId: 'nx-counter-consult', hasImage: true }) === 'bind', 'a fit is bound');
ok(reuse.phase === 'ready' && reuse.assetId === 'nx-counter-consult' && reuse.keepPicture === false, 'a fit is shown');

const variant = interpretVisualResolve({
  decision: 'VARIANT_STORED',
  assetId: 'approved-still',
  variantAssetId: 'approved-still-1',
  paidProviderCalls: 0,
  reasonCodes: ['VARIANT_STORED', 'APPROVED_LOCK'],
}, 'variant');
ok(variant.phase === 'variant' && variant.keepPicture === true && variant.assetId === 'approved-still', 'a variant does not replace the approved still');

const handoff = interpretVisualResolve({
  decision: VISUAL_PROVIDER_HANDOFF,
  paidProviderCalls: 0,
  providerCallAuthorized: false,
  hasImage: false,
}, 'create');
ok(handoff.phase === 'needs-provider' && handoff.offerPaid === true && handoff.keepPicture === true, 'high-quality stills stop at the provider handoff');
ok(interpretVisualResolve({ decision: VISUAL_PROVIDER_HANDOFF, paidProviderCalls: 1 }, 'create').offerPaid !== true, 'a paid call is not continued');

ok(execBlock.includes('applyVisualCreate'), 'Tạo hình enters shared visual resolve before Production OS');
ok(execBlock.includes('createProductionRequest'), 'the existing CREATE_IMAGE request remains for the explicit provider step');
ok(execBlock.includes('offerPaid') && execBlock.includes('tenantImage'), 'provider handoff still requires the existing tenant permit');
ok(visualFn.includes('resolveVisualShot') && visualFn.includes('searchOnly: true'), 'the click searches before it creates');
ok(visualFn.includes('regenerate: opts.regenerate'), 'variant asks the resolver to store a new asset');
ok(!visualFn.includes('/series/still') && !visualFn.includes('generateContentSeriesStill'), 'visual resolve does not reopen the legacy still');
ok(hydrate.includes('readVisualShotBinding') && !hydrate.includes('resolveVisualShot'), 'reload reads the binding and does not resolve');
ok(!execBlock.includes('/series/still') && !execBlock.includes('generateContentSeriesStill'), 'OS handler still has no legacy still');
ok(workspace.includes('data-visual-phase') && workspace.includes('Tạo biến thể'), 'the desk shows the visual phase and a variant action');
ok(api.includes("'/content/video-engine/visual-intelligence/resolve'") && api.includes('searchOnly'), 'admin client posts the shared resolve contract');
ok(api.includes('LEGACY / NON-AUTHORITY') && api.includes("'/content/series/still'"), 'legacy still stays classified');

if (fail.length) {
  console.error(`VISUAL_STUDIO_STILL FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('VISUAL_STUDIO_STILL PASS FAIL=0 (no provider)');
