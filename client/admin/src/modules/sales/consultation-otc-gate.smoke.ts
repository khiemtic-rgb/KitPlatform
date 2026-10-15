import { blocksAutomaticOtcSuggestions } from './consultation-otc-gate';

const fail: string[] = [];

if (blocksAutomaticOtcSuggestions('none', 'likely')) fail.push('adult likely');
if (blocksAutomaticOtcSuggestions('none', 'insufficient')) fail.push('insufficient still suggestable');
if (!blocksAutomaticOtcSuggestions('stop_sale', 'needs_evaluation')) fail.push('stop');
if (!blocksAutomaticOtcSuggestions('refer_medical', 'needs_evaluation')) fail.push('refer medical');
if (blocksAutomaticOtcSuggestions('refer_pharmacist', 'likely')) fail.push('pharmacist still suggestable');
if (!blocksAutomaticOtcSuggestions('caution', 'needs_evaluation')) fail.push('red flag hold');

if (fail.length) {
  console.error('consultation-otc-gate.smoke FAIL', fail);
  process.exit(1);
}
console.log('consultation-otc-gate.smoke OK');
