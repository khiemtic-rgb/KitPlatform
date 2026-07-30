import type { FamilyDnaCard } from '@/shared/api/family-os.api';
import type { FamilyPlaybookId } from '@/shared/value/family-playbook-ids';

/**
 * Wave B — Blueprint-first voice helpers.
 * AI surfaces must cite DNA when present; never invent values/stage.
 */

export type BlueprintBecause = {
  /** One sentence: "Vì nhà bạn…" — null when sparse. */
  becauseVi: string | null;
  /** Stable tokens for tests / analytics. */
  domains: Array<'values' | 'focus' | 'stage' | 'next' | 'calibration'>;
};

/** PB0020 — nhà chưa hydrate Blueprint. */
export type SparseDnaCta = {
  playbookId: FamilyPlaybookId;
  titleVi: string;
  doThisVi: string;
  reasonVi: string;
  moodLineVi: string;
};

function joinVi(parts: string[], sep = ' · '): string {
  return parts.map((p) => p.trim()).filter(Boolean).join(sep);
}

/** True when DNA missing or empty — do not invent "Vì nhà bạn…". */
export function isBlueprintSparse(dna: FamilyDnaCard | null | undefined): boolean {
  if (!dna) return true;
  if (!dna.hasBlueprint) return true;
  const hasSignal =
    (dna.valuesLabelsVi?.some((x) => x.trim()) ?? false) ||
    (dna.focusLabelsVi?.some((x) => x.trim()) ?? false) ||
    Boolean(dna.stageLabelVi?.trim()) ||
    Boolean(dna.nextStepVi?.trim()) ||
    Boolean(dna.coachTipVi?.trim());
  return !hasSignal;
}

/** One CTA when Blueprint sparse — PB0020. */
export function sparseDnaCta(): SparseDnaCta {
  return {
    playbookId: 'PB0020',
    titleVi: 'Cho Famixa biết về nhà mình',
    doThisVi: 'Hoàn tất DNA gia đình (~2 phút)',
    reasonVi:
      'Famixa chưa biết giá trị và nhịp sống của nhà bạn, nên chỉ nói được điều chung chung. Trả lời 2 phút là gợi ý sẽ đúng nhà mình.',
    moodLineVi: 'Famixa cần 2 phút để hiểu nhà mình trước khi tư vấn.',
  };
}

/** Build the mandatory "because" clause from DNA card (R Blueprint). */
export function becauseFromDna(dna: FamilyDnaCard | null | undefined): BlueprintBecause {
  if (isBlueprintSparse(dna)) {
    return { becauseVi: null, domains: [] };
  }

  const values = (dna!.valuesLabelsVi ?? []).map((x) => x.trim()).filter(Boolean).slice(0, 2);
  const focus = (dna!.focusLabelsVi ?? []).map((x) => x.trim()).filter(Boolean).slice(0, 2);
  const stage = dna!.stageLabelVi?.trim() || '';
  const domains: BlueprintBecause['domains'] = [];

  const chunks: string[] = [];
  if (values.length) {
    domains.push('values');
    chunks.push(
      values.length === 1
        ? `chọn giá trị ${values[0]}`
        : `chọn ${values.join(' & ')}`,
    );
  }
  if (focus.length) {
    domains.push('focus');
    chunks.push(
      focus.length === 1
        ? `đang tập trung ${focus[0]}`
        : `đang tập trung ${focus.join(' & ')}`,
    );
  }

  if (chunks.length > 0) {
    return {
      becauseVi: `Vì nhà bạn ${chunks.join(' và ')}.`,
      domains,
    };
  }

  if (stage) {
    domains.push('stage');
    return {
      becauseVi: `Vì nhà bạn đang ở giai đoạn ${stage}.`,
      domains,
    };
  }

  const next = dna!.nextStepVi?.trim();
  if (next) {
    domains.push('next');
    return {
      becauseVi: `Vì bước tiếp theo của nhà bạn: ${next}`,
      domains,
    };
  }

  const cal = dna!.calibrationLabelVi?.trim() || dna!.growthBalanceLabelVi?.trim();
  if (cal) {
    domains.push('calibration');
    return {
      becauseVi: `Vì nhịp nhà bạn: ${cal}.`,
      domains,
    };
  }

  return { becauseVi: null, domains: [] };
}

/** Append because to an existing basedOn / reason line (idempotent). */
export function withBlueprintBecause(
  base: string | null | undefined,
  dna: FamilyDnaCard | null | undefined,
): string {
  const { becauseVi } = becauseFromDna(dna);
  const core = (base ?? '').trim();
  if (!becauseVi) return core;
  if (core.includes('Vì nhà bạn') || core.includes(becauseVi)) return core || becauseVi;
  return core ? `${core} · ${becauseVi}` : becauseVi;
}

/** Prefer DNA next step / coach tip when local doThis is generic. */
export function preferDnaNextStep(
  doThis: string,
  dna: FamilyDnaCard | null | undefined,
): string {
  if (isBlueprintSparse(dna)) return doThis;
  const tip = dna?.coachTipVi?.trim() || dna?.nextStepVi?.trim();
  if (!tip || dna?.isTeaser) return doThis;
  // Chỉ thay một lời khuyên thực sự chung chung bằng bước DNA có thể hành động.
  // Không dùng các từ rộng như "một lần": chúng xuất hiện trong nhiều hướng dẫn
  // cụ thể (nhắc một lần, chờ 15 phút) và từng làm mất hành động cần làm ngay.
  const genericAdvice =
    /^(chọn một việc nóng nhất|mở hôm nay để xem việc cần ưu tiên|chọn một việc duy nhất)/i.test(
      doThis.trim(),
    );
  const actionableDnaTip =
    /^(hãy|thử|chọn|giữ|đặt|nhắc|hỏi|đứng|cùng|dành|bắt đầu|mở|tạm|ưu tiên|hoàn tất|thiết lập)(?:\s|[,:;.!?—-])/i.test(
      tip,
    );
  if (genericAdvice && actionableDnaTip && tip.length > 12) {
    return tip;
  }
  return doThis;
}

export function dnaCaptionForHealth(
  dna: FamilyDnaCard | null | undefined,
): string | null {
  if (isBlueprintSparse(dna)) {
    return sparseDnaCta().reasonVi;
  }
  const { becauseVi } = becauseFromDna(dna);
  if (becauseVi) return becauseVi;
  if (dna?.hasBlueprint && dna.stageLabelVi) {
    return joinVi([`Giai đoạn ${dna.stageLabelVi}`, ...(dna.focusLabelsVi ?? []).slice(0, 1)]);
  }
  return null;
}
