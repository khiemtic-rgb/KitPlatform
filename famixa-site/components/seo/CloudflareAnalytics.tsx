import Script from 'next/script';

/** Cloudflare Web Analytics beacon — set NEXT_PUBLIC_CF_WEB_ANALYTICS_TOKEN khi build. */
export function CloudflareAnalytics() {
  const token = process.env.NEXT_PUBLIC_CF_WEB_ANALYTICS_TOKEN?.trim();
  if (!token) return null;

  return (
    <Script
      defer
      src="https://static.cloudflareinsights.com/beacon.min.js"
      data-cf-beacon={JSON.stringify({ token })}
      strategy="afterInteractive"
    />
  );
}
