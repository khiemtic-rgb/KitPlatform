import { PlansPage } from '@/components/specialty/PlansPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('vi', 'plans');
}

export default function Page() {
  return <PlansPage locale="vi" />;
}
