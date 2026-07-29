import type { Metadata } from 'next';
import { Inter } from 'next/font/google';
import { ThemeProvider } from '@/components/providers/ThemeProvider';
import { SITE_NAME, absoluteUrl, DEFAULT_OG_IMAGE } from '@/lib/site';
import './globals.css';

const inter = Inter({
  subsets: ['latin'],
  variable: '--font-inter',
  display: 'swap',
});

export const metadata: Metadata = {
  title: {
    default: `${SITE_NAME} — AI Human Growth OS for Families`,
    template: `%s | ${SITE_NAME}`,
  },
  description:
    'AI giúp con tự giác. Cha mẹ không còn phải nhắc mỗi ngày. Famixa — người bạn đồng hành AI cho gia đình Việt.',
  metadataBase: new URL('https://famixa.vn'),
  keywords: ['Famixa', 'AI gia đình', 'nuôi dạy con', 'tự giác', 'Family OS', 'Fami'],
  authors: [{ name: 'KIT Technology' }],
  openGraph: {
    title: `${SITE_NAME} — AI Human Growth OS for Families`,
    description: 'AI giúp con tự giác. Cha mẹ không còn phải nhắc mỗi ngày.',
    url: absoluteUrl('/vi/'),
    siteName: SITE_NAME,
    images: [{ url: absoluteUrl(DEFAULT_OG_IMAGE), width: 1200, height: 630, alt: SITE_NAME }],
    locale: 'vi_VN',
    type: 'website',
  },
  twitter: {
    card: 'summary_large_image',
    title: `${SITE_NAME} — AI Human Growth OS for Families`,
    description: 'AI giúp con tự giác. Cha mẹ không còn phải nhắc mỗi ngày.',
    images: [absoluteUrl(DEFAULT_OG_IMAGE)],
  },
  robots: {
    index: true,
    follow: true,
  },
  alternates: {
    canonical: absoluteUrl('/vi/'),
  },
};

const themeBoot = `(function(){try{var k='famixa-theme';var m=localStorage.getItem(k)||'light';var d=m==='dark'||(m==='system'&&window.matchMedia('(prefers-color-scheme: dark)').matches);document.documentElement.setAttribute('data-theme',d?'dark':'light');}catch(e){document.documentElement.setAttribute('data-theme','light');}})();`;

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="vi" className={inter.variable} suppressHydrationWarning>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeBoot }} />
      </head>
      <body className="font-sans antialiased">
        <ThemeProvider>{children}</ThemeProvider>
      </body>
    </html>
  );
}
