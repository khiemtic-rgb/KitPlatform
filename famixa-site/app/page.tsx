'use client';

import { useEffect } from 'react';

export default function HomePage() {
  useEffect(() => {
    window.location.replace('/vi/');
  }, []);

  return (
    <main className="grid min-h-screen place-items-center p-8 text-[var(--color-secondary)]">
      <p>
        Redirecting to{' '}
        <a className="font-bold text-[var(--color-primary)]" href="/vi/">
          /vi/
        </a>
        …
      </p>
    </main>
  );
}
