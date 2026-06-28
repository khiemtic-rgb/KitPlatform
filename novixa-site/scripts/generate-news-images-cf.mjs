import { loadDotEnv } from './load-env.mjs';
import { generateAllCfNewsImages } from './cf-ai-news-image.mjs';

loadDotEnv();

const force = process.argv.includes('--force');
await generateAllCfNewsImages({ force });
