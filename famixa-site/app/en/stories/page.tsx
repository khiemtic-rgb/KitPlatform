import { StoriesPage } from '@/components/specialty/StoriesPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('en', 'stories');
}

export default function Page() {
  return <StoriesPage locale="en" />;
}
