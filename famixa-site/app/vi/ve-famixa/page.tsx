import { AboutPage } from '@/components/specialty/AboutPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('vi', 'about');
}

export default function Page() {
  return <AboutPage locale="vi" />;
}
