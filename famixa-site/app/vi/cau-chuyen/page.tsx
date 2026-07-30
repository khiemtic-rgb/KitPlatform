import { StoriesPage } from '@/components/specialty/StoriesPage';
import { buildSpecialtyMetadata } from '@/lib/specialty-metadata';

export function generateMetadata() {
  return buildSpecialtyMetadata('vi', 'stories');
}

export default function Page() {
  return <StoriesPage locale="vi" />;
}
