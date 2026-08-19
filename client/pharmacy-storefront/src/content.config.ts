import { defineCollection, z } from 'astro:content';
import { glob } from 'astro/loaders';

/** Kit Marketing → Astro Git (`contentFormat: pharmacy`) writes here. */
const kienThuc = defineCollection({
  loader: glob({ pattern: '**/*.md', base: './src/content/kien-thuc' }),
  schema: z.object({
    title: z.string(),
    description: z.string().optional(),
    pubDate: z.coerce.date(),
    image: z.string().optional(),
    category: z.string().optional(),
    readingMinutes: z.number().optional(),
  }),
});

export const collections = {
  'kien-thuc': kienThuc,
};
