import { LegalPage } from '@/components/specialty/LegalPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('vi', 'terms');
}

export default function Page() {
  return <LegalPage locale="vi" kind="terms" />;
}
