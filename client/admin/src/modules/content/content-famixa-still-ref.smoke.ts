import { shrinkStillDataUrl } from './content-famixa-still-ref';

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

setTimeout(() => {
  if (fail.length) {
    console.error('STILL REF FAIL');
    for (const f of fail) console.error(` - ${f}`);
    process.exit(1);
  }
  console.log('STILL REF PASS');
}, 50);
