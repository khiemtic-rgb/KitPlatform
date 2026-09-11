import type { ContentTopic, ContentVariant } from '@/shared/api/content.api';

/** Hard rules aligned with server SealImagePrompt (no text in pixels). */
const IMAGE_HARD_RULES =
  'HARD RULES: photorealistic photograph. Zero written language in the frame. '
  + 'No letters, numbers, logos, captions, watermarks, posters, price tags, product labels, '
  + 'or shop signs with readable text. Storefront boards and shelf labels must be blank, '
  + 'blurred, or turned away. Screens show graphics only, never words. '
  + 'Do not invent brand names. Vietnamese and English text are both forbidden.';

function clip(text: string, max: number): string {
  const t = text.replace(/\s+/g, ' ').trim();
  if (t.length <= max) return t;
  return `${t.slice(0, max - 1).trim()}…`;
}

/**
 * Prompt nhân viên copy sang ChatGPT / Gemini / Midjourney… để tạo ảnh đúng bài,
 * không cần gọi «Tạo ảnh» trên hệ thống (tiết kiệm phí).
 */
export function buildStaffImagePrompt(
  topic: Pick<ContentTopic, 'title' | 'pillar' | 'goal' | 'bodyOutline' | 'brandName'>,
  variants: ContentVariant[],
): string {
  const beats = variants
    .slice(0, 4)
    .map((v) => {
      const head = [v.kind, v.title?.trim()].filter(Boolean).join(': ');
      const body = clip(v.bodyMarkdown ?? '', 280);
      return body ? `- ${head} — ${body}` : `- ${head}`;
    })
    .filter((line) => line.length > 2)
    .join('\n');

  const sceneSeed =
    'Photorealistic documentary still of the exact human moment this article argues — '
    + 'one subject, one action, emotion first. Not a generic shop interior or stock handshake.';

  const lines = [
    'You are an image generator. Create ONE photorealistic still that illustrates this article claim.',
    '',
    `Brand: ${topic.brandName}`,
    `Title / theme (mood only — do NOT paint these words): ${topic.title.trim()}`,
    topic.pillar ? `Pillar / series: ${topic.pillar}` : null,
    topic.goal ? `Goal: ${topic.goal}` : null,
    topic.bodyOutline?.trim() ? `Outline: ${clip(topic.bodyOutline, 400)}` : null,
    '',
    'ARTICLE BEATS (illustrate this, not a generic brand photo):',
    beats || `- (no draft yet) Base the scene on the title: ${topic.title.trim()}`,
    '',
    `Scene direction: ${sceneSeed}`,
    '',
    IMAGE_HARD_RULES,
    '',
    'Output: a single photograph, natural lighting, pharmacy / retail operations context if relevant, Vietnamese setting when appropriate — but ZERO readable text in the image.',
  ];

  return lines.filter((x) => x !== null).join('\n');
}
