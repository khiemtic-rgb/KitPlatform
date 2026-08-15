import { isRecent, type LocalListing } from './api';

export const EVENT_CATS = [
  { id: 'conference', label: 'Hội thảo', re: /hội thảo|hội nghị|conference|aimcs/i },
  { id: 'workshop', label: 'Workshop', re: /workshop/i },
  { id: 'music', label: 'Âm nhạc', re: /âm nhạc|đêm nhạc|concert|ca nhạc/i },
  { id: 'sport', label: 'Thể thao', re: /thể thao|giải đấu|bóng đá/i },
  { id: 'fair', label: 'Ngày hội', re: /ngày hội|festival|hội chợ/i },
  { id: 'fun', label: 'Giải trí', re: /giải trí/i },
] as const;

export const EVENT_WHEN = [
  { id: 'upcoming', label: 'Sắp diễn ra' },
  { id: 'month', label: 'Tháng này' },
  { id: 'week', label: 'Tuần này' },
] as const;

function blob(item: LocalListing): string {
  return [item.title, item.summary, item.organizationName, item.placeText, item.category, item.workingTime]
    .filter(Boolean)
    .join(' ');
}

export function eventCatOf(item: LocalListing): string {
  if (item.category === 'conference') return 'conference';
  if (item.category && EVENT_CATS.some((c) => c.id === item.category)) return item.category;
  const text = blob(item);
  return EVENT_CATS.find((c) => c.re.test(text))?.id ?? '';
}

export function eventCatLabel(id?: string | null): string {
  return EVENT_CATS.find((c) => c.id === id)?.label ?? 'Sự kiện';
}

export function eventPlaceKey(item: LocalListing): string {
  const p = (item.placeText ?? '').trim();
  if (!p) return (item.organizationName ?? '').trim();
  return p.split(/[·•|]/)[0]?.trim() || p;
}

export function eventPlaces(items: LocalListing[]): string[] {
  return [...new Set(items.map(eventPlaceKey).filter((p) => p.length > 1 && p.length < 70))]
    .sort((a, b) => a.localeCompare(b, 'vi'));
}

export function eventCatsIn(items: LocalListing[]): { id: string; label: string; count: number }[] {
  const counts = new Map<string, number>();
  for (const item of items) {
    const id = eventCatOf(item);
    if (!id) continue;
    counts.set(id, (counts.get(id) ?? 0) + 1);
  }
  return EVENT_CATS
    .map((c) => ({ id: c.id, label: c.label, count: counts.get(c.id) ?? 0 }))
    .filter((c) => c.count > 0);
}

export function eventTags(item: LocalListing): string[] {
  const text = blob(item);
  const tags: string[] = [];
  if (/ai|robot|iot|công nghệ|mechatronic|ieee/i.test(text)) tags.push('Công nghệ');
  if (/\bai\b|artificial/i.test(text)) tags.push('AI');
  if ((item.audience ?? []).some((a) => a.toLowerCase() === 'student') || /sinh viên|\bsv\b/i.test(text)) {
    tags.push('Sinh viên');
  }
  const cat = eventCatLabel(eventCatOf(item));
  if (cat !== 'Sự kiện' && !tags.includes(cat)) tags.unshift(cat);
  return tags.slice(0, 4);
}

export function eventDateBadge(iso?: string | null): { day: string; mon: string } | null {
  if (!iso) return null;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return null;
  return {
    day: String(d.getDate()).padStart(2, '0'),
    mon: `Th${d.getMonth() + 1}`,
  };
}

export function isUpcoming(item: LocalListing): boolean {
  const end = item.endAt || item.startAt;
  if (!end) return true;
  const t = new Date(end).getTime();
  return !Number.isNaN(t) && t >= Date.now();
}

export function featuredEvents(items: LocalListing[], limit = 4): LocalListing[] {
  return items
    .filter((e) => e.startAt && isUpcoming(e))
    .sort((a, b) => new Date(a.startAt ?? 0).getTime() - new Date(b.startAt ?? 0).getTime())
    .slice(0, limit);
}

export function filterEvents(
  items: LocalListing[],
  opts: { cat?: string; when?: string; place?: string },
): LocalListing[] {
  const now = new Date();
  return items.filter((item) => {
    if (opts.cat && eventCatOf(item) !== opts.cat) return false;
    if (opts.place && eventPlaceKey(item) !== opts.place) return false;
    if (opts.when === 'upcoming' && !isUpcoming(item)) return false;
    if (opts.when === 'month' || opts.when === 'week') {
      if (!item.startAt) return false;
      const s = new Date(item.startAt);
      if (Number.isNaN(s.getTime())) return false;
      if (opts.when === 'month' && (s.getMonth() !== now.getMonth() || s.getFullYear() !== now.getFullYear())) {
        return false;
      }
      if (opts.when === 'week') {
        const start = new Date(now);
        start.setDate(now.getDate() - now.getDay());
        start.setHours(0, 0, 0, 0);
        const end = new Date(start);
        end.setDate(start.getDate() + 7);
        if (s < start || s >= end) return false;
      }
    }
    return true;
  });
}

export function isNewEvent(item: LocalListing): boolean {
  return isRecent(item.publishedAt || item.lastCheckedAt, 48);
}

export function formatEventWhenFull(iso?: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '';
  const time = d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' });
  const weekday = ['Chủ nhật', 'Thứ 2', 'Thứ 3', 'Thứ 4', 'Thứ 5', 'Thứ 6', 'Thứ 7'][d.getDay()];
  const date = d.toLocaleDateString('vi-VN');
  return `${time} · ${weekday}, ${date}`;
}

export function eventFormat(item: LocalListing): string {
  const t = blob(item).toLowerCase();
  if (/hybrid/.test(t) || (/trực tuyến|online/.test(t) && /trực tiếp|offline|ictu/.test(t) && /paris|hybrid/.test(t))) {
    return 'Hybrid';
  }
  if (/hybrid/.test(t)) return 'Hybrid';
  if (/trực tuyến|online/.test(t)) return 'Trực tuyến';
  if (/trực tiếp|offline/.test(t)) return 'Trực tiếp';
  return '';
}

export function eventFee(item: LocalListing): string {
  const text = blob(item);
  const euro = text.match(/(\d[\d.,]*)\s*€/);
  if (euro) return `${euro[1]}€`;
  const phi = text.match(/phí[^.]{0,48}?(\d[\d.,]*\s*(?:€|euro|usd|đ))/i);
  return phi?.[1]?.trim() ?? '';
}

export function eventTopics(item: LocalListing): string[] {
  const text = `${item.summary ?? ''} ${item.requirements ?? ''}`;
  const after = text.match(/(?:nộp bài|hướng|chủ đề)[:：]\s*([^.]+)/i);
  if (!after?.[1]) return [];
  return after[1]
    .split(/,|\/| và /)
    .map((s) => s.replace(/\.$/, '').trim())
    .filter((s) => s.length > 2 && s.length < 70)
    .slice(0, 8);
}

export function eventDeadlineBits(item: LocalListing): string[] {
  if (!item.workingTime) return [];
  return item.workingTime
    .split(/[·•]|(?:;\s+)/)
    .map((s) => s.trim())
    .filter((s) => s.length > 2);
}

export function similarEvents(current: LocalListing, all: LocalListing[], limit = 3): LocalListing[] {
  const cat = eventCatOf(current);
  const others = all.filter((e) => e.id !== current.id);
  const ranked = others.filter((e) => eventCatOf(e) === cat);
  return (ranked.length > 0 ? ranked : others).slice(0, limit);
}
