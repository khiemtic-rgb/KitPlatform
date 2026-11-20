import type { LearningBadge, LearningModuleProgress } from '@/shared/api/learning.api';
import { resolveBadgeVisual, resolveLessonMeta } from '@/modules/learning/learning-module-meta';

/** Ước lượng tổng badge có ý nghĩa trong catalog (hiển thị x/N). */
export const COMPETENCY_CATALOG_SIZE = 24;

export type BadgeTier = 'achievement' | 'certification' | 'milestone';

export type ClassifiedBadge = LearningBadge & {
  tier: BadgeTier;
  tierLabel: string;
  featuredScore: number;
  isLevelCompleteNoise: boolean;
  visual: ReturnType<typeof resolveBadgeVisual>;
};

export type StriveGoal = {
  key: string;
  title: string;
  /** Điểm quiz lần gần nhất (thật); null = chưa có dữ liệu. */
  pct: number | null;
  hint: string;
};

function daysAgo(iso: string) {
  return (Date.now() - new Date(iso).getTime()) / (1000 * 60 * 60 * 24);
}

/** complete_l* = cột mốc lộ trình cũ — không «đeo ngực», chỉ sưu tập. */
export function isLevelCompleteNoise(badgeCode: string) {
  return /^complete_l\d$/i.test(badgeCode.trim());
}

export function classifyBadge(b: LearningBadge): ClassifiedBadge {
  const code = b.badgeCode.toLowerCase();
  const visual = resolveBadgeVisual(b.badgeCode, b.title);
  const levelNoise = isLevelCompleteNoise(code);

  let tier: BadgeTier = 'certification';
  let featuredScore = 40;

  if (
    code.includes('mentor') ||
    code.includes('tenure') ||
    code.includes('customer_praise') ||
    code.includes('zero_error') ||
    code.includes('close_streak') ||
    code === 'perfect_l5'
  ) {
    tier = 'achievement';
    featuredScore = 100;
    if (code.includes('mentor')) featuredScore = 110;
    if (code.includes('customer')) featuredScore = 105;
    if (code.includes('tenure')) featuredScore = 95;
    if (code.includes('zero_error')) featuredScore = 90;
    if (code.includes('close_streak')) featuredScore = 88;
  } else if (code.startsWith('perfect_')) {
    tier = 'certification';
    featuredScore = 75;
  } else if (levelNoise || /^l\d$/i.test(code)) {
    tier = 'milestone';
    featuredScore = 15;
  } else if (code.includes('crm') || code.includes('fefo') || code.includes('pos') || code.includes('gpp')) {
    tier = 'certification';
    featuredScore = 70;
  }

  const tierLabel =
    tier === 'achievement' ? 'Thành tựu' : tier === 'certification' ? 'Chứng nhận' : 'Cột mốc';

  return {
    ...b,
    tier,
    tierLabel,
    featuredScore,
    isLevelCompleteNoise: levelNoise,
    visual,
  };
}

export function buildCompetencyProfile(
  badges: LearningBadge[],
  modules: LearningModuleProgress[],
  opts?: { recognitionCount30d?: number },
) {
  const classified = badges.map(classifyBadge).sort((a, b) => {
    const t = new Date(b.earnedAt).getTime() - new Date(a.earnedAt).getTime();
    return t;
  });

  const meaningful = classified.filter((b) => !b.isLevelCompleteNoise);
  const achievements = meaningful.filter((b) => b.tier === 'achievement');
  const certifications = meaningful.filter((b) => b.tier === 'certification');
  const milestones = classified.filter((b) => b.tier === 'milestone' || b.isLevelCompleteNoise);

  const featured = [...meaningful]
    .sort((a, b) => b.featuredScore - a.featuredScore || +new Date(b.earnedAt) - +new Date(a.earnedAt))
    .slice(0, 5);

  const recent = classified
    .filter((b) => daysAgo(b.earnedAt) <= 30)
    .slice(0, 5);

  const passedLevels = modules
    .filter((m) => m.status === 'passed')
    .map((m) => m.levelCode.toUpperCase())
    .sort();
  const currentLevel =
    passedLevels.length > 0 ? passedLevels[passedLevels.length - 1] : modules[0]?.levelCode?.toUpperCase() ?? '—';

  const passed = modules.filter((m) => m.status === 'passed').length;
  /** % bài đã đạt trong lộ trình được giao; null khi chưa có lộ trình. */
  const competencyScore = modules.length ? Math.round((100 * passed) / modules.length) : null;

  const striving: StriveGoal[] = modules
    .filter((m) => m.status !== 'passed')
    .slice(0, 3)
    .map((m) => {
      const meta = resolveLessonMeta({
        moduleCode: m.moduleCode,
        levelCode: m.levelCode,
        title: m.title,
      });
      const lastScore = m.quizAttempts > 0 && m.scorePct != null ? m.scorePct : null;
      return {
        key: m.moduleId,
        title: `${m.levelCode} · ${m.title}`,
        pct: lastScore,
        hint:
          lastScore != null
            ? `Điểm lần gần nhất · còn ~${meta.minutes} phút`
            : `Chưa có dữ liệu · còn ~${meta.minutes} phút`,
      };
    });

  return {
    classified,
    meaningful,
    achievements,
    certifications,
    milestones,
    featured,
    recent,
    striving,
    summary: {
      achievementCount: achievements.length,
      certificationCount: certifications.length,
      milestoneCount: milestones.length,
      unlockedCount: meaningful.length,
      catalogSize: COMPETENCY_CATALOG_SIZE,
      currentLevel,
      competencyScore,
      recognitionCount30d: opts?.recognitionCount30d ?? 0,
    },
  };
}

export type CompetencyProfile = ReturnType<typeof buildCompetencyProfile>;
