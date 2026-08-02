/**
 * Care / bond copy tone by VN school age band (from DOB years).
 * Keep teen voice calmer — no "việc nhỏ" / toddler cheer for 13+.
 */

export type CareAgeBand = 'preschool' | 'primary' | 'lower_secondary' | 'upper_secondary';

export function ageYearsFromDob(dob?: string | null, asOf = new Date()): number | null {
  if (!dob) return null;
  const d = new Date(dob.slice(0, 10) + 'T12:00:00');
  if (Number.isNaN(d.getTime())) return null;
  let years = asOf.getFullYear() - d.getFullYear();
  const m = asOf.getMonth() - d.getMonth();
  if (m < 0 || (m === 0 && asOf.getDate() < d.getDate())) years--;
  return years < 0 ? null : years;
}

export function careAgeBandFromYears(years: number | null | undefined): CareAgeBand {
  if (years == null) return 'primary';
  if (years <= 6) return 'preschool';
  if (years <= 9) return 'primary';
  if (years <= 12) return 'lower_secondary';
  return 'upper_secondary';
}

export function careAgeBandFromDob(dob?: string | null): CareAgeBand {
  return careAgeBandFromYears(ageYearsFromDob(dob));
}

export function isTeenCareBand(band: CareAgeBand): boolean {
  return band === 'lower_secondary' || band === 'upper_secondary';
}

/** Bond strip eyebrow — avoid "riêng cho" sounding childish for teens. */
export function bondStripEyebrowVi(shortName: string, band: CareAgeBand): string {
  if (isTeenCareBand(band)) return `Gắn kết · dành cho ${shortName}`;
  return `Gắn kết · riêng cho ${shortName}`;
}

export function livingFoxyForBand(
  shortName: string,
  band: CareAgeBand,
  remaining: number,
  teamRemaining: number,
  teamComplete: boolean,
  justCelebrated: boolean,
  nextTitle?: string,
): string {
  const teen = isTeenCareBand(band);
  if (justCelebrated) {
    return teen
      ? `${shortName} xong việc rồi — báo bố/mẹ một câu ngắn là đủ.`
      : `Wowww!! ${shortName} làm tốt quá! Bố/mẹ sẽ rất vui khi nghe tin này.`;
  }
  if (teamComplete) {
    return teen
      ? `Cả nhà xong ngày — chờ bố/mẹ xác nhận phần thưởng nhóm.`
      : `🎉 Cả nhà xong rồi — nhờ bố/mẹ xác nhận phần thưởng nhóm nhé!`;
  }
  if (remaining === 0 && teamRemaining > 0) {
    return teen
      ? `${shortName} xong phần mình. Đội còn ${teamRemaining} việc — có muốn cổ vũ anh/chị em?`
      : `${shortName} xong phần mình rồi! Cả đội còn ${teamRemaining} việc — Foxy cổ vũ cả nhà!`;
  }
  if (remaining === 0) {
    return teen
      ? `Hôm nay ${shortName} đã giữ nhịp tốt.`
      : `${shortName} ơi, hôm nay con đã giúp cả nhà rất nhiều!`;
  }
  if (remaining === 1) {
    return teen
      ? `Còn 1 việc nữa — ${shortName} tự quyết nhịp nhé.`
      : `${shortName} ơi. Con chỉ còn 1 việc nữa. Mình cùng cố nhé!`;
  }
  if (remaining === 2) {
    return teen
      ? `Còn 2 việc — chọn cái dễ trước cũng được.`
      : `${shortName} ơi. Con chỉ còn 2 việc nữa. Mình cùng cố nhé!`;
  }
  if (nextTitle) {
    return teen
      ? `Tiếp theo: «${nextTitle}».`
      : `${shortName} ơi, tiếp theo mình làm «${nextTitle}» giúp cả nhà nhé!`;
  }
  return teen
    ? `${shortName} — giữ một việc tiếp theo là đủ.`
    : `${shortName} ơi, hôm nay mình giúp cả nhà hoàn thành ngày nhé!`;
}

export function parentVoiceCardEyebrowVi(shortName: string, band: CareAgeBand): string {
  if (isTeenCareBand(band)) return `Lời gửi ${shortName}`;
  return `Lời riêng cho ${shortName}`;
}

export function tasksFoxyBannerVi(
  shortName: string,
  band: CareAgeBand,
  doNowCount: number,
  remaining: number,
): string {
  const teen = isTeenCareBand(band);
  if (doNowCount > 0) {
    return teen
      ? `Còn ${doNowCount} việc cần làm ngay.`
      : `${shortName} ơi, còn ${doNowCount} việc cần làm ngay — Foxy cổ vũ con! 💪`;
  }
  if (remaining > 0) {
    return teen
      ? `Đang giữ nhịp — còn ${remaining} việc trong ngày.`
      : `${shortName} đang làm rất tốt! Còn ${remaining} việc nữa để cả nhà xong ngày nhé!`;
  }
  return teen
    ? `${shortName} đã xong hết hôm nay.`
    : `${shortName} đã xong hết hôm nay — nghỉ ngơi vui vẻ nhé!`;
}

export function homeDoneEmptyCopyVi(
  band: CareAgeBand,
  teamComplete: boolean,
  teamRemaining: number,
): string {
  const teen = isTeenCareBand(band);
  if (teamComplete) {
    return teen
      ? 'Cả nhà xong ngày — nhờ bố/mẹ xác nhận phần thưởng nhóm.'
      : 'Cả nhà xong rồi — nhờ bố/mẹ xác nhận phần thưởng nhóm nhé!';
  }
  if (teamRemaining > 0) {
    return teen
      ? `Đội còn ${teamRemaining} việc — phần của bạn đã xong.`
      : `Cả đội còn ${teamRemaining} việc — con đã giúp cả nhà rất nhiều.`;
  }
  return teen ? 'Hôm nay bạn đã giữ nhịp tốt.' : 'Foxy ôm bạn cái! Nghỉ ngơi vui vẻ nhé.';
}

export function cheerPreviewAudienceVi(band: CareAgeBand, targetShort: string): string {
  if (isTeenCareBand(band)) return `${targetShort} sẽ thấy`;
  return 'Em sẽ thấy';
}

/** Kid → parent reply CTAs — always with subject (hiếu thảo). */
export function kidThanksParentCtaVi(parentRole: string): string {
  const role = (parentRole || 'bố mẹ').trim() || 'bố mẹ';
  return `Con cảm ơn ${role}`;
}

export function kidSeenParentCtaVi(parentRole: string): string {
  const role = (parentRole || 'bố mẹ').trim() || 'bố mẹ';
  return `Con đã xem lời ${role}`;
}

export function cheerOfferCopyForBand(
  _shortName: string,
  targetShort: string,
  teamRemaining: number,
  band: CareAgeBand,
  triggerBody?: string | null,
): string {
  if (triggerBody?.trim()) return triggerBody.trim();
  if (isTeenCareBand(band)) {
    return `Còn ${teamRemaining} việc trong đội — gửi một lời ngắn cho ${targetShort}?`;
  }
  return `Cả nhà còn ${teamRemaining} việc — muốn cổ vũ ${targetShort}?`;
}

export function parentVoiceHomeLineForBand(
  fromName: string,
  shortName: string,
  band: CareAgeBand,
): string {
  const who = fromName.trim() || 'bố/mẹ';
  if (isTeenCareBand(band)) {
    return `${who} có lời gửi ${shortName} — mở thẻ bên dưới khi sẵn sàng.`;
  }
  return `${who} có lời riêng cho ${shortName} — mở thẻ bên dưới nhé.`;
}
