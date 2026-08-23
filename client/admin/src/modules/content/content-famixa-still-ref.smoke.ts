import {
  aspectMatchesRunway,
  containDrawRect,
  coverCropRect,
  pixelsMatchRunway,
  runwayFrameSize,
  shouldContainFit,
  shrinkStillDataUrl,
} from './content-famixa-still-ref';

const fail: string[] = [];
const small = 'data:image/png;base64,iVBORw0KGgo=';
void shrinkStillDataUrl(small).then((out) => {
  if (out !== small) fail.push('small dataUrl must pass through');
});
void shrinkStillDataUrl('').then((out) => {
  if (out) fail.push('empty must drop');
});
void shrinkStillDataUrl('https://x/a.png').then((out) => {
  if (out) fail.push('http must drop');
});

const land = runwayFrameSize('16:9');
if (land.width !== 1280 || land.height !== 720) fail.push('16:9 frame is 1280×720');
const port = runwayFrameSize('9:16');
if (port.width !== 720 || port.height !== 1280) fail.push('9:16 frame is 720×1280');
if (aspectMatchesRunway(1280, 720, '16:9') !== true) fail.push('exact 16:9 matches');
if (aspectMatchesRunway(1344, 768, '16:9') !== true) fail.push('1344×768 is 16:9 slack');
if (pixelsMatchRunway(1344, 768, '16:9')) fail.push('1344×768 must not pass official pixels');
if (!pixelsMatchRunway(1280, 720, '16:9')) fail.push('1280×720 is official');
if (aspectMatchesRunway(1024, 1024, '16:9')) fail.push('square must not match 16:9');
const crop = coverCropRect(1024, 1024, 1280, 720);
if (crop.sw !== 1024) fail.push('square→16:9 keeps full width');
if (Math.abs(crop.sh - Math.round(1024 / (1280 / 720))) > 2) fail.push('square→16:9 crops height');
if (crop.sy < 0) fail.push('cover crop sy must stay in frame');
if (!shouldContainFit(1344, 768, 720, 1280, 2)) fail.push('2 people landscape→9:16 must contain');
if (shouldContainFit(1344, 768, 720, 1280, 1)) fail.push('1 person may still cover-crop');
const box = containDrawRect(1344, 768, 720, 1280);
if (box.dw > 720 || box.dh > 1280) fail.push('contain must fit inside dest');
if (box.dw < 700) fail.push('landscape contain in 9:16 keeps full width');

setTimeout(() => {
  if (fail.length) {
    console.error('STILL REF FAIL');
    for (const f of fail) console.error(` - ${f}`);
    process.exit(1);
  }
  console.log('STILL REF PASS');
}, 50);
