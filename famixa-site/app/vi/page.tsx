import { LandingPage, buildLandingMetadata } from '@/components/LandingPage';

export async function generateMetadata() {
  return buildLandingMetadata('vi');
}

export default async function ViLandingPage() {
  return <LandingPage locale="vi" />;
}
