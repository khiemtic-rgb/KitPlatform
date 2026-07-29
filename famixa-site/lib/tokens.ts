/** Design tokens — single source for Theme + CMS theming hooks */
export const tokens = {
  color: {
    light: {
      primary: '#1FA45A',
      dark: '#103B2B',
      bg: '#F9FBF8',
      bgElevated: '#FFFFFF',
      text: '#1D1D1F',
      secondary: '#666666',
      border: '#E7ECE8',
      muted: '#EAF5EE',
    },
    dark: {
      primary: '#3DD68C',
      dark: '#0A1F17',
      bg: '#0C1410',
      bgElevated: '#15201A',
      text: '#F2F5F3',
      secondary: '#A3B1A9',
      border: '#24332B',
      muted: '#1A2921',
    },
  },
  radius: { 8: 8, 12: 12, 20: 20, 32: 32 },
  space: { 8: 8, 12: 12, 16: 16, 24: 24, 32: 32, 48: 48, 64: 64, 96: 96 },
  shadow: '0 10px 30px rgba(0,0,0,.06)',
  type: { hero: 64, section: 40, card: 22, body: 18, small: 14 },
  maxWidth: 1180,
  breakpoints: { sm: 640, md: 768, lg: 1024, xl: 1280 },
} as const;

export type ThemeMode = 'light' | 'dark' | 'system';
