import {
  defaultQualityTier,
  qualityTierOf,
  readQualityTier,
  writeQualityTier,
} from './famixa-quality-tier';

const shot = 'EP01-SC01-SH01';
if (defaultQualityTier('IMAGE') !== '0.5K') throw new Error('image draft');
if (defaultQualityTier('VIDEO') !== '5') throw new Error('video draft');
if (defaultQualityTier('VOICE') !== 'eleven_v3') throw new Error('voice package');
if (defaultQualityTier('LIPSYNC') !== 'sync-lipsync-v3') throw new Error('lipsync package');
writeQualityTier(shot, 'IMAGE', '4K');
if (readQualityTier(shot, 'IMAGE') !== '4K') throw new Error('saved high image');
if (qualityTierOf('IMAGE', '4K').detail.includes('0,151') === false) throw new Error('4k price');
writeQualityTier(shot, 'IMAGE', 'nope');
if (readQualityTier(shot, 'IMAGE') !== '4K') throw new Error('unknown tier ignored');
writeQualityTier(shot, 'VIDEO', '10');
if (readQualityTier(shot, 'VIDEO') !== '10') throw new Error('saved long video');
console.log('famixa-quality-tier smoke PASS');
