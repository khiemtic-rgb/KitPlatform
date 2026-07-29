import type { Metadata } from 'next';
import { getLandingContent } from '@/lib/cms/getLanding';
import { absoluteUrl } from '@/lib/site';
import { Navbar } from '@/components/sections/Navbar';
import { Footer } from '@/components/sections/Footer';
import { StatsDashboard } from '@/components/stats/StatsDashboard';
import { Container } from '@/components/ui/Container';

export const metadata: Metadata = {
  title: 'Thống kê truy cập',
  description: 'Thống kê lượt truy cập famixa.vn (chỉ dành cho quản trị).',
  robots: { index: false, follow: false },
  alternates: {
    canonical: absoluteUrl('/vi/thong-ke/'),
  },
};

export default async function ThongKePage() {
  const c = await getLandingContent('vi');

  return (
    <>
      <Navbar content={c.nav} brand={c.brand} appUrl={c.appUrl} locale="vi" />
      <main className="stats-page">
        <Container>
          <StatsDashboard />
        </Container>
      </main>
      <Footer content={c.footer} brand={c.brand} appUrl={c.appUrl} locale="vi" />
    </>
  );
}
