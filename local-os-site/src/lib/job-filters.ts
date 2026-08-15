import { isRecent, type LocalListing } from './api';

export const JOB_TYPES = [
  { id: 'part_time', label: 'Part-time' },
  { id: 'full_time', label: 'Full-time' },
  { id: 'weekend', label: 'Cuối tuần' },
  { id: 'internship', label: 'Thực tập' },
] as const;

function blob(item: LocalListing): string {
  return [
    item.title,
    item.summary,
    item.organizationName,
    item.placeText,
    item.employmentType,
    item.category,
    item.requirements,
    ...(item.audience ?? []),
  ]
    .filter(Boolean)
    .join(' ')
    .toLowerCase();
}

export function isPartTime(item: LocalListing): boolean {
  if (item.employmentType === 'part_time') return true;
  return /part[- ]?time|làm thêm|ca tối/.test(blob(item));
}

export function isStudentJob(item: LocalListing): boolean {
  if ((item.audience ?? []).some((a) => a.toLowerCase() === 'student')) return true;
  return /sinh viên|\bsv\b/.test(blob(item));
}

export function isIntern(item: LocalListing): boolean {
  if (item.category === 'internship' || item.employmentType === 'internship') return true;
  return /thực tập/.test(blob(item));
}

export function isRemote(item: LocalListing): boolean {
  return /tại nhà|remote|work from home|online/.test(blob(item));
}

export function isNewJob(item: LocalListing): boolean {
  return isRecent(item.publishedAt || item.lastCheckedAt, 48);
}

export function jobTypeOf(item: LocalListing): string {
  if (isIntern(item)) return 'internship';
  if (item.employmentType && JOB_TYPES.some((t) => t.id === item.employmentType)) {
    return item.employmentType;
  }
  if (isPartTime(item)) return 'part_time';
  return item.employmentType || '';
}

export function jobTags(item: LocalListing): string[] {
  const tags: string[] = [];
  if (isIntern(item)) tags.push('Thực tập');
  else if (item.employmentType === 'full_time') tags.push('Full-time');
  else if (item.employmentType === 'weekend') tags.push('Cuối tuần');
  if (isPartTime(item) && !tags.includes('Part-time')) tags.push('Part-time');
  if (isStudentJob(item)) tags.push('Sinh viên');
  if (isRemote(item)) tags.push('Làm tại nhà');
  return tags.slice(0, 4);
}

export function jobPlaces(items: LocalListing[]): string[] {
  return [...new Set(items.map((i) => (i.placeText ?? '').trim()).filter((p) => p.length > 1 && p.length < 60))]
    .sort((a, b) => a.localeCompare(b, 'vi'));
}

export function filterJobs(
  items: LocalListing[],
  opts: { type?: string; place?: string; pay?: string; chip?: string },
): LocalListing[] {
  return items.filter((item) => {
    if (opts.type && jobTypeOf(item) !== opts.type) return false;
    if (opts.place && (item.placeText ?? '').trim() !== opts.place) return false;
    if (opts.pay === 'has' && !item.salaryText) return false;
    if (opts.chip === 'new' && !isNewJob(item)) return false;
    if (opts.chip === 'part_time' && !isPartTime(item)) return false;
    if (opts.chip === 'student' && !isStudentJob(item)) return false;
    if (opts.chip === 'remote' && !isRemote(item)) return false;
    return true;
  });
}

export function featuredJobs(items: LocalListing[], limit = 3): LocalListing[] {
  return items
    .filter((i) =>
      ['OFFICIAL', 'SOURCE_TRUSTED', 'VERIFIED'].includes((i.trust ?? '').toUpperCase()),
    )
    .slice(0, limit);
}

export function splitJobLines(text?: string | null): string[] {
  if (!text) return [];
  const parts = text
    .split(/\n+|•|·|(?:;\s+)/)
    .map((s) => s.replace(/^\s*[-–]\s*/, '').replace(/\.$/, '').trim())
    .filter((s) => s.length > 2);
  return parts.length >= 2 ? parts : [text.replace(/\s+/g, ' ').trim()].filter(Boolean);
}

export function similarJobs(current: LocalListing, all: LocalListing[], limit = 4): LocalListing[] {
  const others = all.filter((j) => j.id !== current.id);
  const type = jobTypeOf(current);
  const ranked = others.filter((j) => jobTypeOf(j) === type || isStudentJob(j) === isStudentJob(current));
  return (ranked.length > 0 ? ranked : others).slice(0, limit);
}

export function jobFacts(item: LocalListing): { label: string; value: string }[] {
  const type = JOB_TYPES.find((t) => t.id === jobTypeOf(item))?.label;
  const rows: { label: string; value: string }[] = [];
  if (type) rows.push({ label: 'Hình thức', value: type });
  if (item.workingTime) rows.push({ label: 'Thời gian', value: item.workingTime });
  if (item.placeText) rows.push({ label: 'Địa điểm', value: item.placeText });
  if (item.sourceName) rows.push({ label: 'Nguồn', value: item.sourceName });
  return rows;
}

export function jobStats(items: LocalListing[]): { label: string; count: number }[] {
  return [
    { label: 'Tất cả việc làm', count: items.length },
    { label: 'Mới (48 giờ)', count: items.filter(isNewJob).length },
    { label: 'Part-time', count: items.filter(isPartTime).length },
    { label: 'Sinh viên', count: items.filter(isStudentJob).length },
  ].filter((row) => row.label === 'Tất cả việc làm' || row.count > 0);
}
