import type { Metadata } from 'next';
import { GuideHubPage } from '@/components/guide/GuideHubPage';
import { getGuideHub } from '@/lib/guide/content';
import { absoluteUrl } from '@/lib/site';

export function generateMetadata(): Metadata {
  const guide = getGuideHub();
  return {
    title: guide.title,
    description: 'Hướng dẫn Famixa đơn giản, giúp gia đình bắt đầu, tạo nhịp sinh hoạt và dùng ứng dụng mỗi ngày.',
    alternates: {
      canonical: absoluteUrl('/vi/huong-dan/'),
    },
  };
}

export default function Page() {
  return <GuideHubPage />;
}
