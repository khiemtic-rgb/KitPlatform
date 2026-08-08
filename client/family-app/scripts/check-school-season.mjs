/**
 * Quick node check for School Season phase logic (no build step).
 * Run: node scripts/check-school-season.mjs
 */
import assert from 'node:assert/strict';

function minutesOf(hhmm) {
  const [h, m] = hhmm.split(':').map(Number);
  return h * 60 + m;
}

function phaseAt(schedule, weekday, hhmm) {
  if (!schedule.seasonOn || schedule.mode === 'off') return 'season_off';
  if (!schedule.weekdays.includes(weekday)) return 'weekend';
  const t = minutesOf(hhmm);
  const start = minutesOf(schedule.schoolStart);
  const quietEnd = minutesOf(
    schedule.hasExtraClass && schedule.extraEnd ? schedule.extraEnd : schedule.schoolEnd,
  );
  const schoolEnd = minutesOf(schedule.schoolEnd);
  if (t < start) return 'before_school';
  if (t < quietEnd) return 'at_school';
  if (t < Math.max(schoolEnd + 30, minutesOf('19:30'))) return 'after_school';
  return 'evening';
}

const full = {
  seasonOn: true,
  mode: 'full',
  weekdays: [1, 2, 3, 4, 5],
  schoolStart: '07:00',
  schoolEnd: '16:30',
  hasExtraClass: true,
  extraEnd: '18:30',
};

assert.equal(phaseAt(full, 6, '10:00'), 'weekend');
assert.equal(phaseAt(full, 1, '06:30'), 'before_school');
assert.equal(phaseAt(full, 1, '09:00'), 'at_school');
assert.equal(phaseAt(full, 1, '17:00'), 'at_school'); // still in học thêm
assert.equal(phaseAt(full, 1, '18:45'), 'after_school');
assert.equal(phaseAt(full, 1, '20:00'), 'evening');

const off = { ...full, seasonOn: false };
assert.equal(phaseAt(off, 1, '09:00'), 'season_off');

console.log('check-school-season: ok');
