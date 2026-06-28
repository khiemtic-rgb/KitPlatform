import { generateAllNewsImages } from './news-image-lib.mjs';

const forceSvg = process.argv.includes('--svg-only');
const force = process.argv.includes('--force');

if (force) process.env.FORCE_NEWS_IMAGES = '1';

await generateAllNewsImages({ forceSvg });
