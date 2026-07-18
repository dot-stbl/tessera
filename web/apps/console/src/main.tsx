import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from '@tanstack/react-router';
import '@/shared/lib/i18n';
import { router } from './router';
import { PreferencesProvider } from '@/shared/lib/preferences-provider';
import './index.css';

const rootElement = document.getElementById('root');
if (!rootElement) throw new Error('Root element #root not found');

createRoot(rootElement).render(
  <StrictMode>
    <PreferencesProvider>
      <RouterProvider router={router} />
    </PreferencesProvider>
  </StrictMode>,
);