import { generateAllAiNewsImages } from './ai-news-image.mjs';

const force = process.argv.includes('--force');
await generateAllAiNewsImages({ force });
