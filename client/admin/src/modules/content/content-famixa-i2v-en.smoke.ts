import { englishI2vMotion, englishI2vRetry, i2vPromptIsEnglish } from './content-famixa-i2v-en';

const fail: string[] = [];
const vi = englishI2vMotion('Ngoài cổng trường — chiều. Minh gọi mẹ.', 5);
if (!/school gate/i.test(vi)) fail.push('school gate beat missing');
if (!i2vPromptIsEnglish(vi)) fail.push('Vietnamese leaked into I2V prompt');
if (!/Minh/.test(vi)) fail.push('Minh should stay in English prompt');

const en = 'A boy stands at a school gate and blinks. Same clothes. 5 seconds.';
if (englishI2vMotion(en) !== en) fail.push('English motion must pass through');

const kitchen = englishI2vMotion('Linh đang chuẩn bị cơm.', 5);
if (!/meal|kitchen/i.test(kitchen)) fail.push('kitchen/meal beat missing');

const takePaper = englishI2vMotion('Linh nhận lấy. Linh: Tám?', 5);
if (!/paper|receives/i.test(takePaper)) fail.push('receive-paper beat missing');
if (!i2vPromptIsEnglish(takePaper)) fail.push('Vietnamese leaked on nhận lấy');

const home = englishI2vMotion('Buổi tối. Nam: Anh về rồi.', 5);
if (!/evening|arrives home/i.test(home)) fail.push('Nam homecoming beat missing');

const retry = englishI2vRetry(takePaper);
if (retry === takePaper) fail.push('retry must change the prompt');
if (!i2vPromptIsEnglish(retry)) fail.push('retry leaked Vietnamese');

if (fail.length) {
  console.error('I2V EN FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('I2V EN PASS');
