import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Select, Space, Tag, Typography } from 'antd';
import { fetchScriptLibrary, fetchScriptLibraryItem, type ContentScriptLibraryItem } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';

/**
 * Kịch bản viết sẵn của kênh (file .txt trong docs/marketing/<kênh>/kich-ban).
 * Nạp nguyên văn vào ô kịch bản của phim. Không gọi AI.
 */
export function ScriptLibraryCard({
  brandCode,
  currentScript,
  hasPictures,
  onLoad,
}: {
  brandCode: string;
  currentScript: string;
  hasPictures: boolean;
  onLoad: (text: string, item: ContentScriptLibraryItem) => void;
}) {
  const { modal, message } = App.useApp();
  const [items, setItems] = useState<ContentScriptLibraryItem[]>([]);
  const [note, setNote] = useState('');
  const [picked, setPicked] = useState<string>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');

  const reload = () => {
    if (!brandCode) return;
    setError('');
    void fetchScriptLibrary(brandCode)
      .then((data) => {
        setItems(data.items);
        setNote(data.message || '');
      })
      .catch((reason) => setError(apiErrorMessage(reason, 'Không đọc được thư viện kịch bản.')));
  };

  useEffect(reload, [brandCode]);

  const chosen = items.find((item) => item.id === picked);

  const load = async () => {
    if (!picked) return;
    setBusy(true);
    setError('');
    try {
      const item = await fetchScriptLibraryItem(brandCode, picked);
      const text = (item.body || '').trim();
      if (!text) {
        setError('File kịch bản trống.');
        return;
      }
      const apply = () => {
        onLoad(text, item);
        message.success(`Đã nạp ${item.code}. Bấm Phân tích kịch bản để tiếp tục.`);
      };
      if (currentScript.trim() && currentScript.trim() !== text) {
        modal.confirm({
          title: 'Thay kịch bản trong phim?',
          zIndex: 4000,
          content: hasPictures
            ? 'Phim đã có ảnh. Câu nào đổi sẽ phải tạo lại ảnh; ảnh và video đã tạo vẫn được giữ trong hồ sơ phim.'
            : 'Kịch bản đang có trong ô sẽ được thay bằng bản trong thư viện.',
          okText: 'Thay',
          cancelText: 'Giữ nguyên',
          onOk: apply,
        });
        return;
      }
      apply();
    } catch (reason) {
      setError(apiErrorMessage(reason, 'Không mở được kịch bản.'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card size="small" title="Kịch bản đã viết sẵn" style={{ marginBottom: 16 }} extra={<Button size="small" onClick={reload}>Tải lại</Button>}>
      <Space direction="vertical" style={{ width: '100%' }} size={8}>
        {items.length ? (
          <Space wrap style={{ width: '100%' }}>
            <Select
              style={{ minWidth: 420 }}
              placeholder="Chọn kịch bản"
              value={picked}
              onChange={setPicked}
              options={items.map((item) => ({
                value: item.id,
                label: `${item.code} · ${item.title} · ${item.status}`,
              }))}
            />
            <Button type="primary" loading={busy} disabled={!picked} onClick={() => void load()}>
              Nạp vào phim
            </Button>
          </Space>
        ) : null}
        {chosen ? (
          <Space wrap>
            <Tag color={chosen.approved ? 'green' : 'gold'}>{chosen.status}</Tag>
            <Typography.Text type="secondary">
              {chosen.scenes} cảnh · {chosen.lines} dòng{chosen.updated ? ` · cập nhật ${chosen.updated}` : ''}
            </Typography.Text>
            {chosen.docsUrl ? (
              <Typography.Link href={chosen.docsUrl} target="_blank" rel="noreferrer">
                Mở Docs
              </Typography.Link>
            ) : null}
          </Space>
        ) : null}
        {chosen && !chosen.approved ? (
          <Alert type="warning" showIcon message="Kịch bản chưa duyệt. Nạp để xem thử được, nhưng chưa nên tạo giọng và video." />
        ) : null}
        {note ? <Typography.Text type="secondary">{note}</Typography.Text> : null}
        {error ? <Alert type="error" showIcon message={error} /> : null}
      </Space>
    </Card>
  );
}
