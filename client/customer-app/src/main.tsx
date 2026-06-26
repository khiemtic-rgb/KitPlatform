import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { setupServiceWorkerRegistration } from './shared/push/sw-registration';
import { AppProviders } from '@/app/providers';
import '@/styles/index.css';

setupServiceWorkerRegistration();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AppProviders />
  </StrictMode>,
);
