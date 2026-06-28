import { pickFrom, hashSlug } from './slug-hash.mjs';
import { pickTheme } from './news-image-lib.mjs';

const SCENES = [
  'wide shot of a bright modern Vietnamese pharmacy interior, pharmacist arranging medicine boxes on shelves, natural daylight',
  'close-up of expiry date labels on pharmaceutical cartons, organized warehouse shelf, shallow depth of field',
  'pharmacist using tablet POS at checkout counter, customer queue, clean retail environment',
  'multi-branch pharmacy dashboard on laptop screen, stock charts and branch map in background blur',
  'inventory audit with clipboard and barcode scanner in pharmacy back room, realistic editorial photo',
  'FEFO concept: worker picking oldest batch first from refrigerated medicine cabinet, professional lighting',
  'CRM loyalty card handover at pharmacy counter, warm friendly service moment, photorealistic',
  'GPP compliance checklist on desk next to sealed medicine packages, clinical tidy workspace',
  'pharmacy owner reviewing revenue report in office, charts on monitor, confident business mood',
  'chain expansion: two pharmacy storefronts on a Vietnamese street, morning light, urban context',
  'digital transformation: replacing paper ledgers with cloud ERP on computer in small pharmacy office',
  'AI assistant concept: subtle holographic data overlay above pharmacy shelf, futuristic but realistic',
  'near-expiry alert sticky notes on shelf edge, pharmacist reviewing stock, urgent but calm atmosphere',
  'KPI dashboard projected on wall in pharmacy meeting room, team discussion, corporate editorial style',
  'cold chain storage with temperature monitor, vaccine and medicine boxes, professional healthcare logistics',
  'customer consultation at pharmacy counter with pharmacist pointing to medicine leaflet, trust and care',
  'night shift pharmacy with soft lighting, pharmacist closing daily sales report, quiet urban storefront',
  'delivery of new medicine cartons at pharmacy loading area, staff checking invoice, logistics scene',
  'pharmacy training session, staff learning POS system, collaborative workplace photography',
  'seasonal flu medicine display end-cap in pharmacy, promotional layout, retail merchandising photo',
  'pharmacist scanning GS1 barcode on medicine box, macro detail, tech-forward operations',
  'empty Excel spreadsheet replaced by mobile inventory app in pharmacist hands, change narrative',
  'family-owned pharmacy exterior with green cross sign, welcoming neighborhood context, Vietnam',
];

const MOODS = [
  'optimistic professional editorial photography',
  'clean corporate healthcare marketing photo',
  'warm documentary-style business photography',
  'crisp high-end stock photo aesthetic',
  'contemporary SaaS brand campaign photography',
];

const CAMERA = [
  '35mm lens, soft bokeh',
  '50mm lens, balanced composition',
  '24mm wide environmental shot',
  '85mm portrait-style framing on subject',
];

export function buildNewsImagePrompt({ slug, title, description = '' }) {
  const theme = pickTheme(title, description);
  const scene = pickFrom(slug, SCENES);
  const mood = pickFrom(slug, MOODS);
  const camera = pickFrom(slug, CAMERA);
  const variant = hashSlug(slug) % 997;

  return [
    `Create a unique hero image for a Vietnamese pharmacy management software blog article.`,
    `Article topic: "${title}".`,
    `Visual theme hint: ${theme.label}.`,
    `Scene (variant ${variant}): ${scene}.`,
    `Style: ${mood}, ${camera}.`,
    `Color accents: pharmacy blue #0b4d8c and green #1fa85c subtly in environment.`,
    `Requirements: photorealistic, no text, no letters, no logos, no watermarks, no UI mockups with readable text.`,
    `Leave lower 20% relatively uncluttered for text overlay.`,
  ].join(' ');
}
