import { LandingPage, buildLandingMetadata } from '@/components/LandingPage';

export async function generateMetadata() {
  return buildLandingMetadata('en');
}

export default async function EnLandingPage() {
  return <LandingPage locale="en" />;
}
