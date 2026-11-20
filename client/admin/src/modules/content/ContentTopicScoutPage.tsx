import { useCallback, useEffect, useState } from 'react';
import { Button, Typography, message } from 'antd';
import { isAxiosError } from 'axios';
import {
  acceptTopicScout,
  dismissTopicScout,
  fetchTopicScout,
  runTopicScout,
  type TopicScoutProposal,
} from '@/shared/api/content.api';

function errorText(error: unknown) {
  if (isAxiosError(error)) {
    const data = error.response?.data as { message?: string } | undefined;
    if (data?.message) return data.message;
  }
  return 'Không quét được.';
}

function Lane({
  title,
  rows,
  busy,
  onAccept,
  onDismiss,
}: {
  title: string;
  rows: TopicScoutProposal[];
  busy: string | null;
  onAccept: (id: string) => void;
  onDismiss: (id: string) => void;
}) {
  return (
    <section style={{ flex: '1 1 360px', minWidth: 320 }}>
      <Typography.Title level={4}>{title}</Typography.Title>
      {rows.length === 0 ? <p>Chưa có phiếu.</p> : null}
      {rows.map((row) => (
        <article key={row.id} style={{ border: '1px solid #e5e5e5', padding: 16, marginBottom: 12 }}>
          <Typography.Title level={5} style={{ marginTop: 0 }}>{row.title}</Typography.Title>
          <pre style={{ whiteSpace: 'pre-wrap', fontFamily: 'inherit', margin: 0 }}>{row.brief}</pre>
          {row.status === 'pending' ? (
            <div style={{ display: 'flex', gap: 8, marginTop: 12 }}>
              <Button type="primary" loading={busy === row.id} onClick={() => onAccept(row.id)}>Đưa vào pool</Button>
              <Button loading={busy === row.id} onClick={() => onDismiss(row.id)}>Bỏ</Button>
            </div>
          ) : (
            <p>Đã vào pool.</p>
          )}
        </article>
      ))}
    </section>
  );
}

export function ContentTopicScoutPage() {
  const [rows, setRows] = useState<TopicScoutProposal[]>([]);
  const [loading, setLoading] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);
  const [note, setNote] = useState('');

  const load = useCallback(async () => {
    const data = await fetchTopicScout();
    setRows(data ?? []);
  }, []);

  useEffect(() => {
    void load().catch((error: unknown) => message.error(errorText(error)));
  }, [load]);

  async function run() {
    setLoading(true);
    try {
      const result = await runTopicScout();
      setNote(result.message);
      await load();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setLoading(false);
    }
  }

  async function accept(id: string) {
    setBusy(id);
    try {
      await acceptTopicScout(id);
      await load();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setBusy(null);
    }
  }

  async function dismiss(id: string) {
    setBusy(id);
    try {
      await dismissTopicScout(id);
      await load();
    } catch (error) {
      message.error(errorText(error));
    } finally {
      setBusy(null);
    }
  }

  const vi = rows.filter((row) => row.lane === 'vi');
  const en = rows.filter((row) => row.lane === 'en');

  return (
    <div>
      <p>
        Agent đọc bảng video đang được xem nhiều, không hỏi từ khóa. Phần Việt chỉ lấy video ở Việt Nam.
        Phần Anh chỉ lấy Mỹ, Anh và Úc. Bấm đưa vào pool thì ý mới xuất hiện ở tab Ý tưởng.
      </p>
      <Button type="primary" loading={loading} onClick={() => void run()}>Quét chủ đề</Button>
      {note ? <p>{note}</p> : null}
      <div style={{ display: 'flex', gap: 24, alignItems: 'flex-start', marginTop: 16, flexWrap: 'wrap' }}>
        <Lane title="Việt" rows={vi} busy={busy} onAccept={(id) => void accept(id)} onDismiss={(id) => void dismiss(id)} />
        <Lane title="Anh" rows={en} busy={busy} onAccept={(id) => void accept(id)} onDismiss={(id) => void dismiss(id)} />
      </div>
    </div>
  );
}
