/** Inline SVG scenes for pain-point cards (code mockup, no raster banner). */

export const painIcons = [
  '<path d="M9 11.2a3 3 0 1 0-3-3 3 3 0 0 0 3 3Zm6.4 0a3 3 0 1 0-3-3 3 3 0 0 0 3 3ZM9 12.8c-2.7 0-5.4 1.3-5.4 3.1V18h6.4a5 5 0 0 1-.2-1.3A5.2 5.2 0 0 1 12 12.6 5.8 5.8 0 0 0 9 12.8Zm6.4 0a5.6 5.6 0 0 0-2.2.5 5.2 5.2 0 0 1 2.9 4.3c0 .45-.07.9-.2 1.3H20.4v-1.9c0-1.8-2.7-3.2-5-3.2Z"/>',
  '<path d="M12 4.2a7.8 7.8 0 1 0 7.8 7.8A7.8 7.8 0 0 0 12 4.2Zm0 14a6.2 6.2 0 1 1 6.2-6.2A6.2 6.2 0 0 1 12 18.2Zm.7-9.6H11v4l3.5 2.1.8-1.2-2.6-1.6Z"/>',
  '<path d="M4.5 9.2h3.2l8.8-4.2v14L7.7 14.8H4.5A1.5 1.5 0 0 1 3 13.3v-2.6a1.5 1.5 0 0 1 1.5-1.5Zm12.8-2.8v11.2a4.2 4.2 0 0 0 0-11.2ZM7.2 15.8l1.4 3.4H6.2l1-3.4Z"/>',
  '<path d="M5 18V9.5h2.4V18H5Zm5.2 0V6.5H12.6V18H10.2Zm5.2 0v-5.2H18V18h-2.6ZM4.2 20.2h15.6"/>',
  '<path d="M12 11.2a3.1 3.1 0 1 0-3.1-3.1A3.1 3.1 0 0 0 12 11.2Zm0 1.5c-3.2 0-6.3 1.6-6.3 3.4V18h12.6v-1.9c0-1.8-3.1-3.4-6.3-3.4Z"/>',
  '<path d="M6.5 4.8h11A1.5 1.5 0 0 1 19 6.3v11.4a1.5 1.5 0 0 1-1.5 1.5h-11A1.5 1.5 0 0 1 5 17.7V6.3A1.5 1.5 0 0 1 6.5 4.8Zm1.2 2.9v1.3h8.6V7.7Zm0 2.9v1.3h8.6v-1.3Zm0 2.9v1.3h5.4v-1.3Z"/>',
] as const;

export const painArts = [
  // 1 leave customer + pharmacist + X
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="48" cy="98" rx="28" ry="8" fill="#bfdbfe" opacity=".55"/>
    <ellipse cx="168" cy="98" rx="34" ry="8" fill="#bfdbfe" opacity=".45"/>
    <rect x="132" y="52" width="56" height="40" rx="8" fill="#93c5fd"/>
    <rect x="140" y="40" width="40" height="18" rx="6" fill="#60a5fa"/>
    <circle cx="160" cy="28" r="12" fill="#93c5fd"/>
    <path d="M148 68h24v8h-24z" fill="#dbeafe"/>
    <circle cx="58" cy="34" r="12" fill="#60a5fa"/>
    <path d="M40 50c0-8 8-14 18-14s18 6 18 14v34H40V50z" fill="#3b82f6"/>
    <path d="M36 84h44l-6 14H42l-6-14z" fill="#1d4ed8"/>
    <circle cx="108" cy="42" r="14" fill="#2563eb"/>
    <path d="M102 36l12 12M114 36l-12 12" stroke="#fff" stroke-width="2.5" stroke-linecap="round"/>
  </svg>`,
  // 2 calendar + clock
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="110" cy="102" rx="70" ry="10" fill="#fecdd3" opacity=".55"/>
    <rect x="48" y="28" width="70" height="62" rx="10" fill="#fda4af"/>
    <rect x="48" y="28" width="70" height="18" rx="10" fill="#fb7185"/>
    <rect x="48" y="38" width="70" height="8" fill="#fb7185"/>
    <circle cx="70" cy="24" r="4" fill="#e11d48"/>
    <circle cx="96" cy="24" r="4" fill="#e11d48"/>
    <rect x="62" y="56" width="14" height="14" rx="3" fill="#fff"/>
    <rect x="82" y="56" width="14" height="14" rx="3" fill="#fff"/>
    <rect x="102" y="56" width="14" height="14" rx="3" fill="#fff"/>
    <path d="M85 62l6 6M91 62l-6 6" stroke="#e11d48" stroke-width="2" stroke-linecap="round"/>
    <circle cx="158" cy="62" r="28" fill="#fb7185"/>
    <circle cx="158" cy="62" r="22" fill="#fff1f2"/>
    <path d="M158 48v16l10 6" stroke="#e11d48" stroke-width="2.5" stroke-linecap="round"/>
    <circle cx="158" cy="62" r="2.5" fill="#e11d48"/>
  </svg>`,
  // 3 contact list + ?
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="110" cy="102" rx="72" ry="10" fill="#fed7aa" opacity=".55"/>
    <rect x="42" y="24" width="110" height="68" rx="12" fill="#ffedd5"/>
    <g>
      <circle cx="62" cy="42" r="9" fill="#fb923c"/>
      <rect x="78" y="36" width="54" height="6" rx="3" fill="#fdba74"/>
      <rect x="78" y="46" width="40" height="5" rx="2.5" fill="#fed7aa"/>
      <circle cx="62" cy="68" r="9" fill="#f97316"/>
      <rect x="78" y="62" width="54" height="6" rx="3" fill="#fdba74"/>
      <rect x="78" y="72" width="40" height="5" rx="2.5" fill="#fed7aa"/>
    </g>
    <path d="M168 36c10 0 18 7 18 16 0 12-18 18-18 18s-18-6-18-18c0-9 8-16 18-16z" fill="#f97316"/>
    <text x="168" y="56" text-anchor="middle" fill="#fff" font-size="18" font-family="system-ui,sans-serif" font-weight="700">?</text>
  </svg>`,
  // 4 monitor down + coins
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="110" cy="102" rx="72" ry="10" fill="#ddd6fe" opacity=".55"/>
    <rect x="36" y="26" width="96" height="62" rx="10" fill="#c4b5fd"/>
    <rect x="44" y="34" width="80" height="40" rx="6" fill="#f5f3ff"/>
    <path d="M52 42l18 10 16-8 22 18" stroke="#7c3aed" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/>
    <path d="M96 62l12 0 0-12" stroke="#7c3aed" stroke-width="3" stroke-linecap="round"/>
    <rect x="70" y="88" width="28" height="6" rx="3" fill="#a78bfa"/>
    <ellipse cx="168" cy="78" rx="18" ry="10" fill="#8b5cf6"/>
    <ellipse cx="168" cy="70" rx="18" ry="10" fill="#a78bfa"/>
    <ellipse cx="168" cy="62" rx="18" ry="10" fill="#c4b5fd"/>
    <text x="168" y="66" text-anchor="middle" fill="#5b21b6" font-size="12" font-family="system-ui,sans-serif" font-weight="700">$</text>
  </svg>`,
  // 5 two people + tablet
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="110" cy="102" rx="72" ry="10" fill="#a5f3fc" opacity=".55"/>
    <circle cx="78" cy="36" r="12" fill="#67e8f9"/>
    <path d="M56 54c0-10 10-16 22-16s22 6 22 16v34H56V54z" fill="#06b6d4"/>
    <rect x="86" y="58" width="22" height="28" rx="4" fill="#ecfeff" stroke="#0891b2" stroke-width="2"/>
    <circle cx="148" cy="38" r="12" fill="#22d3ee"/>
    <path d="M128 56c0-10 9-16 20-16s20 6 20 16v32h-40V56z" fill="#0891b2"/>
    <path d="M138 70h20" stroke="#ecfeff" stroke-width="3" stroke-linecap="round"/>
  </svg>`,
  // 6 docs + calculator + clock
  `<svg viewBox="0 0 220 120" fill="none" xmlns="http://www.w3.org/2000/svg" aria-hidden="true">
    <ellipse cx="110" cy="102" rx="72" ry="10" fill="#bbf7d0" opacity=".55"/>
    <rect x="40" y="30" width="48" height="58" rx="6" fill="#86efac" transform="rotate(-8 64 59)"/>
    <rect x="58" y="34" width="48" height="58" rx="6" fill="#dcfce7" stroke="#16c784" stroke-width="2"/>
    <path d="M68 48h28M68 58h28M68 68h20" stroke="#16c784" stroke-width="2" stroke-linecap="round"/>
    <rect x="120" y="48" width="36" height="42" rx="6" fill="#16c784"/>
    <rect x="126" y="54" width="24" height="10" rx="2" fill="#ecfdf5"/>
    <rect x="126" y="68" width="8" height="8" rx="1.5" fill="#bbf7d0"/>
    <rect x="138" y="68" width="8" height="8" rx="1.5" fill="#bbf7d0"/>
    <rect x="150" y="68" width="8" height="8" rx="1.5" fill="#bbf7d0"/>
    <circle cx="176" cy="44" r="20" fill="#4ade80"/>
    <circle cx="176" cy="44" r="15" fill="#f0fdf4"/>
    <path d="M176 34v12l8 4" stroke="#16a34a" stroke-width="2.2" stroke-linecap="round"/>
  </svg>`,
] as const;
