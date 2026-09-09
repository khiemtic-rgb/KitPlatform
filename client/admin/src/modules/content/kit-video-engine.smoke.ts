import {
  canTransitionProduction,
  canTransitionShot,
  holdIfNoAction,
  KIT_VIDEO_ENGINE,
  productionAfterShotFailure,
} from './kit-video-engine';
import { FAMIXA_PROJECT, FAMIXA_UNIVERSE, mapFamixaBuildToEngine } from './kit-video-famixa-adapter';

const fail: string[] = [];

if (KIT_VIDEO_ENGINE !== 'KIT-VIDEO-ENGINE-V1') fail.push('engine id');
if (!canTransitionProduction('DRAFT', 'SCRIPT_APPROVED').ok) fail.push('draft → script');
if (canTransitionProduction('DRAFT', 'FINAL').ok) fail.push('must block DRAFT → FINAL');
if (canTransitionProduction('SCRIPT_APPROVED', 'VIDEO_GENERATION').ok) fail.push('must block skip to video');
if (!canTransitionShot('READY', 'KF_GENERATING').ok) fail.push('shot ready → kf');
if (canTransitionShot('DRAFT', 'VIDEO_READY').ok) fail.push('must block shot skip to video');
if (holdIfNoAction(false) !== 'HOLD') fail.push('no action = HOLD');
if (holdIfNoAction(true) !== 'READY') fail.push('has action = READY');

const isolated = productionAfterShotFailure('VIDEO_GENERATION', [
  { state: 'VIDEO_READY' },
  { state: 'FAILED', failed: true },
  { state: 'KF_APPROVED' },
]);
if (isolated.episodeFailed) fail.push('one failed shot must not fail episode');
if (isolated.runStatus !== 'PARTIALLY_COMPLETE') fail.push('run status PARTIALLY_COMPLETE');
if (isolated.productionState !== 'VIDEO_GENERATION') fail.push('production state stays');
if (!isolated.othersContinue) fail.push('other shots continue');

if (FAMIXA_PROJECT !== 'FAMIXA' || FAMIXA_UNIVERSE !== 'FAMILY_A') fail.push('Famixa is a project code, not the engine');
if (mapFamixaBuildToEngine('draft') !== 'DRAFT') fail.push('adapter draft');
if (mapFamixaBuildToEngine('in_prod') !== 'KEYFRAME_GENERATION') fail.push('adapter in_prod');
if (mapFamixaBuildToEngine('final') !== 'FINAL') fail.push('adapter final');

if (fail.length) {
  console.error('KIT VIDEO ENGINE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PASS · CP1 state machine · Famixa is a project');
