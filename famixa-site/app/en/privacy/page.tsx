import { LegalPage } from '@/components/specialty/LegalPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('en', 'privacy');
}

export default function Page() {
  return <LegalPage locale="en" kind="privacy" />;
}
