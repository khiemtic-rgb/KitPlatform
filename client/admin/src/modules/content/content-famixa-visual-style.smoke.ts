import {
  FAMIXA_VISUAL_STYLE,
  FAMIXA_VISUAL_STYLE_ID,
  FAMIXA_VISUAL_STYLE_V1,
  isFamixaVisualStyleDoc,
  styleApproved,
  styleReadyForMaster,
} from './content-famixa-visual-style';

const fail: string[] = [];

if (!isFamixaVisualStyleDoc(FAMIXA_VISUAL_STYLE_V1)) fail.push('style id');
if (FAMIXA_VISUAL_STYLE_V1.status !== 'DRAFT') fail.push('style seed stays DRAFT until Director');
if (!FAMIXA_VISUAL_STYLE.notPhotoreal || !FAMIXA_VISUAL_STYLE.designedCharacter) fail.push('not photoreal designed-character');
if (!FAMIXA_VISUAL_STYLE_V1.notCompetingWith.some((x) => /photoreal/i.test(x))) fail.push('must refuse photoreal competition');
if (!FAMIXA_VISUAL_STYLE_V1.forbidden.some((x) => /photoreal|lookalike/i.test(x))) fail.push('forbid real-child likeness');
if (styleReadyForMaster(FAMIXA_VISUAL_STYLE_V1)) fail.push('DRAFT style cannot unlock Master');
if (styleApproved(FAMIXA_VISUAL_STYLE_V1)) fail.push('AI must not mark style APPROVED');
if (FAMIXA_VISUAL_STYLE.summary !== FAMIXA_VISUAL_STYLE_V1.summary) fail.push('compat summary');
if (FAMIXA_VISUAL_STYLE_ID !== 'FAMIXA-VISUAL-STYLE-V1') fail.push('id token');

if (fail.length) {
  console.error('FAMIXA VISUAL STYLE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('FAMIXA VISUAL STYLE PASS · V1 draft · not photoreal · Director must approve');
