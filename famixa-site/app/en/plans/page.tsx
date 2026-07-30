import { PlansPage } from '@/components/specialty/PlansPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('en', 'plans');
}

export default function Page() {
  return <PlansPage locale="en" />;
}
