import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import { LandingPage } from '@/modules/landing/LandingPage';
import { StartPage } from '@/modules/landing/StartPage';
import { SurveyPage } from '@/modules/survey/SurveyPage';
import { ResultsPage } from '@/modules/results/ResultsPage';
import { UnlockPage } from '@/modules/results/UnlockPage';
import { ReportPage } from '@/modules/report/ReportPage';
import { ThankYouPage } from '@/modules/thank-you/ThankYouPage';

export function App() {
  return (
    <ConfigProvider
      locale={viVN}
      theme={{
        token: {
          colorPrimary: '#0f766e',
          borderRadius: 8,
        },
      }}
    >
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<LandingPage />} />
          <Route path="/start" element={<StartPage />} />
          <Route path="/survey/:id" element={<SurveyPage />} />
          <Route path="/results/:id" element={<ResultsPage />} />
          <Route path="/results/:id/unlock" element={<UnlockPage />} />
          <Route path="/report/:id" element={<ReportPage />} />
          <Route path="/thank-you" element={<ThankYouPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </ConfigProvider>
  );
}
