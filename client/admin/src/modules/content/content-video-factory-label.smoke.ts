/** NOVIXA local render labels. Run: npx tsx content-video-factory-label.smoke.ts */
import {
  factoryVideoTag,
  playableMarketingVideoUrl,
  videoSourceForJob,
} from './content-video-factory-label';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const jobId = '11111111-1111-4111-8111-111111111111';
const route = `/api/content/video/media/${jobId}/output.mp4`;
const novixa = {
  id: jobId,
  brandCode: 'novixa',
  templateCode: 'tiktok_45s_hooks',
  provider: 'storyboard_local',
  storyboardJson: '[{"beat":"HOOK","text":"hello"}]',
};

ok(playableMarketingVideoUrl(route) === route, 'relative output route is playable');
ok(playableMarketingVideoUrl('https://cdn.example/render.mp4') === 'https://cdn.example/render.mp4', 'creatomate https mp4 stays playable');
ok(playableMarketingVideoUrl('https://cdn.example/still.jpg') === '', 'image url is not a video');
ok(playableMarketingVideoUrl('https://cdn.example/still.png?x=1') === '', 'png query is not a video');
ok(playableMarketingVideoUrl('') === '', 'empty url is not a video');

const storyboard = {
  ...novixa,
  status: 'PreparingRender',
  outputUrl: null,
};
ok(factoryVideoTag(storyboard).text === 'Storyboard', 'preparing render is storyboard');
ok(videoSourceForJob(storyboard) === '', 'storyboard does not mount a video');

const readyImage = {
  ...novixa,
  status: 'Ready',
  outputUrl: 'https://image.pollinations.ai/prompt/shelf.jpg',
};
ok(factoryVideoTag(readyImage).text === 'Storyboard', 'novixa image url is not sẵn sàng');
ok(videoSourceForJob(readyImage) === '', 'novixa image url does not mount a video');

const unrelated = {
  ...novixa,
  status: 'Ready',
  outputUrl: 'https://cdn.example/other.mp4',
};
ok(videoSourceForJob(unrelated) === '', 'novixa ignores another job url');
ok(factoryVideoTag(unrelated).text === 'Storyboard', 'unrelated mp4 is not sẵn sàng');

const ready = { ...novixa, status: 'Ready', outputUrl: route };
ok(factoryVideoTag(ready).text === 'Sẵn sàng', 'verified route is sẵn sàng');
ok(videoSourceForJob(ready) === route, 'verified route mounts the video');

const other = {
  id: jobId,
  brandCode: 'famixa',
  templateCode: 'tiktok_45s_hooks',
  provider: 'storyboard_local',
  status: 'Ready',
  outputUrl: null,
  storyboardJson: '[]',
};
ok(factoryVideoTag(other).text === 'Sẵn sàng', 'other brand keeps sẵn sàng');
ok(
  videoSourceForJob({ ...other, outputUrl: 'https://cdn.example/creatomate.mp4' }) === 'https://cdn.example/creatomate.mp4',
  'other brand keeps creatomate player',
);

if (fail.length) {
  console.error(fail.join('\n'));
  process.exit(1);
}
console.log('content-video-factory-label.smoke: PASS');
