/** Nhóm Assessment / AI Health Check — Nhà thuốc & Spa */

export const ASSESSMENT = {
  pharmacy: {
    id: 'pharmacy',
    landingPath: '/vi/health-check/',
    surveyUrl: 'https://survey.novixa.vn',
    label: 'Nhà thuốc',
    tagline: 'Quản trị nhà thuốc thông minh',
    badgeLabel: 'AI HEALTH CHECK',
    displayUrl: 'www.novixa.vn/health-check',
  },
  spa: {
    id: 'spa',
    landingPath: '/vi/spa-health-check/',
    surveyUrl: 'https://survey.novixa.vn/spa',
    label: 'Spa & Clinic',
    tagline: 'Spa, Thẩm mỹ viện & Clinic',
    badgeLabel: 'AI BUSINESS HEALTH CHECK',
    displayUrl: 'www.novixa.vn/spa-health-check',
  },
} as const;

export type AssessmentVertical = keyof typeof ASSESSMENT;

export function getAssessment(vertical: AssessmentVertical) {
  return ASSESSMENT[vertical];
}
