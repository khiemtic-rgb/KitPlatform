import { actingTtsVoiceSettings, inferActingDirection } from './content-famixa-acting-law';
import {
  isChildVoiceLane,
  isKidLibraryVoice,
  isVoiceOnlyRole,
  roleCanonReady,
  voiceLaneForRole,
  voiceSoundsNorthern,
  voicesForLane,
  type SeriesPilotState,
} from './content-famixa-series';

const fail: string[] = [];
const minh = voiceLaneForRole({ characterId: 'CHAR-001', name: 'Minh', title: 'Con' });
if (minh.key !== 'boy') fail.push(`Minh lane ${minh.key}`);
if (!isChildVoiceLane(minh)) fail.push('Minh is 11');

const an = voiceLaneForRole({ characterId: 'CHAR-004', name: 'An', title: 'Bạn' });
if (an.key !== 'boy') fail.push(`An lane ${an.key}`);
if (!isChildVoiceLane(an)) fail.push('An is 11');

const nam = voiceLaneForRole({ characterId: 'CHAR-002', name: 'Nam', title: 'Bố' });
if (nam.key !== 'father') fail.push(`Nam lane ${nam.key}`);

const voLane = voiceLaneForRole({ characterId: 'CHAR-VO', name: 'Lời bình', title: 'Lời bình' });
if (voLane.key !== 'narrator') fail.push(`Lời bình lane ${voLane.key}`);
if (isChildVoiceLane(voLane)) fail.push('narrator is not a child');
const voRole = { id: 'role-CHAR-VO', title: 'Lời bình', name: 'Lời bình', characterId: 'CHAR-VO' };
if (!isVoiceOnlyRole(voRole)) fail.push('CHAR-VO must be voice-only');
const voGraph = {
  roles: [voRole],
  characters: [{ id: 'CHAR-VO', name: 'Lời bình' }],
  runs: {},
} as SeriesPilotState;
if (!roleCanonReady(voGraph, voRole)) fail.push('Lời bình locks Cast without a face photo');

const pool = [
  { voiceId: 'a', name: 'Hanoi Dad', gender: 'male', age: 'middle_aged', vietnamese: true, accent: 'northern' },
  { voiceId: 'b', name: 'Hanoi Boy', gender: 'male', age: 'young', vietnamese: true, accent: 'northern' },
  { voiceId: 'c', name: 'Hanoi Girl', gender: 'female', age: 'young', vietnamese: true, accent: 'northern' },
];
const boys = voicesForLane(pool, minh);
const narrVoices = voicesForLane(pool, voLane);
if (!narrVoices.some((v) => v.voiceId === 'a')) fail.push('narrator must keep Hanoi Dad');
if (narrVoices.some((v) => v.voiceId === 'c')) fail.push('narrator must not list girl voice when dad exists');
const kidsOnly = voicesForLane(
  [
    { voiceId: 'b', name: 'Hanoi Boy', gender: 'male', age: 'young', vietnamese: true, accent: 'northern' },
    { voiceId: 'c', name: 'Hanoi Girl', gender: 'female', age: 'young', vietnamese: true, accent: 'northern' },
  ],
  voLane,
);
if (kidsOnly.length === 0) fail.push('narrator must not be empty when library only has kids');
if (!boys.every((v) => isKidLibraryVoice(v))) fail.push('boy lane leaked adult');
if (!boys.some((v) => v.voiceId === 'b')) fail.push('boy must keep young male');

const girlLane = voiceLaneForRole({ characterId: 'CHAR-099', name: 'Mai', title: 'Bé gái' });
if (girlLane.key !== 'girl') fail.push(`Mai lane ${girlLane.key}`);
const girls = voicesForLane(pool, girlLane);
if (!girls.every((v) => isKidLibraryVoice(v))) fail.push('girl lane leaked adult');
if (!girls.some((v) => v.voiceId === 'c')) fail.push('girl must keep young female');
const anVoices = voicesForLane(pool, an);
if (!anVoices.some((v) => v.voiceId === 'b')) fail.push('An must keep young male');
if (anVoices.some((v) => v.voiceId === 'c')) fail.push('An must not list girl voice');

const southKid = {
  voiceId: 's',
  name: 'Saigon Boy',
  gender: 'male',
  age: 'young',
  vietnamese: true,
  accent: 'southern',
};
const mysteryKid = { voiceId: 'm', name: 'Kid Viet', gender: 'male', age: 'young', vietnamese: true };
const mixed = voicesForLane([...pool, southKid, mysteryKid], an);
if (mixed.some((v) => v.voiceId === 's' || v.voiceId === 'm')) fail.push('An lane must not list Southern or unknown-accent kids');
if (voiceSoundsNorthern(southKid)) fail.push('Saigon boy is not Northern');
if (voiceSoundsNorthern(mysteryKid)) fail.push('unknown accent is not Northern enough');

const kidTts = actingTtsVoiceSettings(inferActingDirection({ text: 'Thì sao?', characterId: 'CHAR-001' }), { speed: 1, stability: 0.55, style: 0 }, { child: true, northern: true });
if (kidTts.speed < 1.03) fail.push('kid speech must be quicker');
if (kidTts.stability > 0.5) fail.push('kid must not be news-anchor stable');
if ((kidTts.similarityBoost ?? 0) < 0.83) fail.push('northern kid must stick to Voice ID');

if (fail.length) {
  console.error('VOICE LANE FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('VOICE LANE PASS');
