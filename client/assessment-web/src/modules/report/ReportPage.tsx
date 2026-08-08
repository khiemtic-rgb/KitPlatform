import { useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Button, Collapse, Spin, Typography, message } from 'antd';
import { fetchReport, fetchReportPdf, triggerPdfDownload, type FullReport } from '@/shared/api/assessment.api';
import { OverallScoreHero } from '@/shared/score/score-display';
import { ReportIntelligenceSections } from '@/modules/report/ReportIntelligenceSections';
import { OwnerPackPanel } from '@/modules/report/OwnerPackPanel';

const { Title, Paragraph } = Typography;

export function ReportPage() {
  const { id = '' } = useParams();
  const [report, setReport] = useState<FullReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [downloadingOwner, setDownloadingOwner] = useState(false);
  const [downloadingFull, setDownloadingFull] = useState(false);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const data = await fetchReport(id);
        if (!cancelled) setReport(data);
      } catch {
        message.error('Báo cáo chưa mở khóa hoặc session hết hạn.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [id]);

  async function downloadPdf(kind: 'owner' | 'consulting', fileName: string) {
    if (!report?.pdf.available) return;
    const setLoadingPdf = kind === 'owner' ? setDownloadingOwner : setDownloadingFull;
    setLoadingPdf(true);
    try {
      const blob = await fetchReportPdf(id, kind);
      triggerPdfDownload(blob, fileName);
      message.success(kind === 'owner' ? 'Đã tải bản dễ đọc cho chủ nhà thuốc.' : 'Đã tải báo cáo đầy đủ.');
    } catch (err: unknown) {
      const msg =
        err instanceof Error
          ? err.message
          : 'Không tải được PDF. Vui lòng thử lại hoặc kiểm tra kết nối mạng.';
      message.error(msg);
    } finally {
      setLoadingPdf(false);
    }
  }

  if (loading) {
    return (
      <div className="page-shell" style={{ textAlign: 'center', paddingTop: '4rem' }}>
        <Spin size="large" />
      </div>
    );
  }

  if (!report) {
    return (
      <div className="page-shell">
        <Paragraph>Không tải được báo cáo.</Paragraph>
        <Link to={`/results/${id}/unlock`}>Mở khóa báo cáo</Link>
      </div>
    );
  }

  const ownerPack = report.intelligence?.ownerPack ?? null;

  return (
    <div className="page-shell page-shell-report">
      <Title level={3}>Kết quả đánh giá nhà thuốc</Title>
      <Paragraph type="secondary">
        Bản dễ hiểu cho chủ nhà thuốc · {report.templateCode}
      </Paragraph>

      <div className="score-card score-card-hero">
        <OverallScoreHero scorePct={report.overallPct} />
      </div>

      {ownerPack ? <OwnerPackPanel pack={ownerPack} /> : null}

      {report.pdf.available ? (
        <div style={{ display: 'grid', gap: 8, marginBottom: '1rem' }}>
          <Button
            type="primary"
            block
            size="large"
            loading={downloadingOwner}
            onClick={() => void downloadPdf('owner', `danh-gia-nha-thuoc-${id.slice(0, 8)}.pdf`)}
          >
            {downloadingOwner ? 'Đang tạo PDF…' : 'Tải bản dễ đọc (khuyến nghị)'}
          </Button>
          <Button
            block
            loading={downloadingFull}
            onClick={() => void downloadPdf('consulting', `kap-bao-cao-day-du-${id.slice(0, 8)}.pdf`)}
          >
            {downloadingFull ? 'Đang tạo PDF đầy đủ…' : 'Tải báo cáo tư vấn đầy đủ (tuỳ chọn)'}
          </Button>
        </div>
      ) : null}

      {report.intelligence ? (
        <Collapse
          items={[
            {
              key: 'full',
              label: 'Xem phân tích chi tiết hơn (biểu đồ & tư vấn đầy đủ)',
              children: (
                <ReportIntelligenceSections report={report} intelligence={report.intelligence} />
              ),
            },
          ]}
        />
      ) : (
        <div className="score-card">
          <Paragraph>
            Báo cáo chi tiết đang được xử lý. Vui lòng tải PDF hoặc tải lại trang sau vài giây.
          </Paragraph>
        </div>
      )}

      <div style={{ marginTop: '1.5rem', textAlign: 'center' }}>
        <Link to="/thank-you">Hoàn tất</Link>
      </div>
    </div>
  );
}
