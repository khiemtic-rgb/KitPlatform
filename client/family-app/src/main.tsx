import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from '@/app/App';
import { enforceLatestAppBuild } from '@/shared/pwa/app-version';
import { setupServiceWorkerUpdates } from '@/shared/pwa/sw-update';
import '@/styles/app.css';

setupServiceWorkerUpdates();
void enforceLatestAppBuild();

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
