import { useEffect, useState } from 'react';
import { Alert, App, Button, Card, Divider, InputNumber, Select, Space, Switch, Tag, Typography } from 'antd';
import { apiErrorMessage } from '@/shared/api/api-error';
import {
  fetchAgentScheduleSettings,
  runAutopilotNow,
  fetchMediaStorage,
  runMediaCleanup,
  saveAgentScheduleSettings,
  setAgentPublishPaused,
  type MediaStorage,
  type AgentScheduleSettings,
} from '@/shared/api/content-agent.api';

const SLOT = /^([01]\d|2[0-3]):[0-5]\d$/;

function mb(bytes: number) {
  const value = bytes / 1024 / 1024;
  return value >= 1024 ? `${(value / 1024).toFixed(1)} GB` : `${value.toFixed(value >= 10 ? 0 : 1)} MB`;
}

/**
 * Cài đặt lịch đăng của một thương hiệu. Agent dùng các số này mỗi lần xếp lịch,
 * kể cả khi Video Studio gửi phim sang (bước 5): xếp tiếp sau các bài đã có lịch.
 */
export function AgentScheduleSettingsCard({
  brandId,
  brandName,
  onLoaded,
}: {
  brandId: string;
  brandName?: string;
  onLoaded?: (settings: AgentScheduleSettings) => void;
}) {
  const { message } = App.useApp();
  const [value, setValue] = useState<AgentScheduleSettings | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [running, setRunning] = useState(false);
  const [report, setReport] = useState<string[] | null>(null);
  const [pausing, setPausing] = useState(false);
  const [storage, setStorage] = useState<MediaStorage | null>(null);
  const [cleaning, setCleaning] = useState(false);

  const loadStorage = () =>
    fetchMediaStorage()
      .then(setStorage)
      .catch(() => setStorage(null));

  useEffect(() => {
    void loadStorage();
  }, []);

  const cleanNow = async () => {
    setCleaning(true);
    try {
      const r = await runMediaCleanup();
      if (r.deletedFiles) message.success(`Đã xoá ${r.deletedFiles} file, giải phóng ${mb(r.freedBytes)}.`);
      else message.info('Không có file nào đến hạn xoá.');
      r.notes.forEach((n) => message.warning(n));
      await loadStorage();
    } catch (reason) {
      message.error(apiErrorMessage(reason, 'Không dọn được file đã đăng'));
    } finally {
      setCleaning(false);
    }
  };

  // Tạm dừng / chạy lại lưu ngay, không cần bấm "Lưu cài đặt" (các ô khác đang sửa dở vẫn giữ).
  const togglePause = async (paused: boolean) => {
    setPausing(true);
    try {
      const next = await setAgentPublishPaused(brandId, paused);
      setValue((cur) => (cur ? { ...cur, publishPaused: next.publishPaused, saved: next.saved } : next));
      onLoaded?.(next);
      message.success(paused ? 'Đã tạm dừng đăng. Bài đã có lịch đứng chờ, không đăng.' : 'Đã chạy lại. Bài tới giờ sẽ đăng tiếp.');
    } catch (reason) {
      message.error(apiErrorMessage(reason, 'Không đổi được trạng thái đăng'));
    } finally {
      setPausing(false);
    }
  };

  const runNow = async () => {
    setRunning(true);
    setReport(null);
    try {
      const r = await runAutopilotNow(brandId);
      setReport(r.steps);
      if (r.wroteTopicId) message.success('Agent đang viết nháp. Bài sẽ hiện ở Bài viết sau khoảng 1–3 phút.');
    } catch (reason) {
      message.error(apiErrorMessage(reason, 'Không chạy được Viết tự động'));
    } finally {
      setRunning(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    setValue(null);
    setError('');
    void fetchAgentScheduleSettings(brandId)
      .then((found) => {
        if (cancelled) return;
        setValue(found);
        onLoaded?.(found);
      })
      .catch((reason) => !cancelled && setError(apiErrorMessage(reason, 'Không tải được cài đặt lịch đăng')));
    return () => {
      cancelled = true;
    };
    // onLoaded cố ý không nằm trong deps: chỉ báo khi đổi thương hiệu.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [brandId]);

  const patch = (next: Partial<AgentScheduleSettings>) => setValue((cur) => (cur ? { ...cur, ...next } : cur));
  const badSlots = (value?.localSlots ?? []).filter((slot) => !SLOT.test(slot));

  const save = async () => {
    if (!value) return;
    if (!value.localSlots.length || badSlots.length) {
      message.error('Khung giờ phải dạng HH:mm, ví dụ 09:00.');
      return;
    }
    setSaving(true);
    try {
      const { saved: _saved, ...body } = value;
      const next = await saveAgentScheduleSettings(brandId, { ...body, localSlots: [...new Set(body.localSlots)].sort() });
      setValue(next);
      onLoaded?.(next);
      message.success('Đã lưu cài đặt lịch đăng.');
    } catch (reason) {
      message.error(apiErrorMessage(reason, 'Không lưu được cài đặt lịch đăng'));
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card
      style={{ marginBottom: 16 }}
      title={
        <Space>
          Cài đặt lịch đăng{brandName ? ` · ${brandName}` : ''}
          {value && !value.saved ? <Tag>Đang dùng mặc định</Tag> : null}
          {value?.publishPaused ? <Tag color="red">Đang tạm dừng đăng</Tag> : null}
        </Space>
      }
      extra={
        <Space>
          <Button
            danger={!value?.publishPaused}
            type={value?.publishPaused ? 'primary' : 'default'}
            loading={pausing}
            disabled={!value}
            onClick={() => void togglePause(!value?.publishPaused)}
          >
            {value?.publishPaused ? 'Chạy lại đăng bài' : 'Tạm dừng đăng'}
          </Button>
          <Button type="primary" loading={saving} disabled={!value} onClick={() => void save()}>Lưu cài đặt</Button>
        </Space>
      }
    >
      {error ? <Alert type="error" showIcon message={error} /> : null}
      {value ? (
        <Space direction="vertical" size={12} style={{ width: '100%' }}>
          {value.publishPaused ? (
            <Alert
              type="warning"
              showIcon
              message="Thương hiệu này đang tạm dừng đăng"
              description={
                'Agent không đăng bài nào của thương hiệu lên YouTube, TikTok, Fanpage, website; không xếp lịch và không viết thêm. ' +
                'Bài đã có lịch vẫn nằm nguyên. Khi bấm "Chạy lại đăng bài", các bài đã quá giờ trong lúc dừng sẽ đăng ngay: ' +
                'xem tab Lịch và huỷ những bài không muốn đăng trước khi chạy lại.'
              }
            />
          ) : null}
          <Space wrap size={16}>
            <span>
              Khung giờ (giờ Việt Nam){' '}
              <Select
                mode="tags"
                style={{ minWidth: 220 }}
                value={value.localSlots}
                status={badSlots.length ? 'error' : undefined}
                tokenSeparators={[',', ' ']}
                onChange={(slots: string[]) => patch({ localSlots: slots.map((s) => s.trim()).filter(Boolean) })}
                options={['07:00', '09:00', '12:00', '19:30', '21:00'].map((s) => ({ value: s }))}
              />
            </span>
            <span>
              Cách nhau tối thiểu{' '}
              <InputNumber min={1} max={48} value={value.minGapHours} onChange={(v) => patch({ minGapHours: v ?? 6 })} /> giờ
            </span>
            <span>
              Xếp trước <InputNumber min={1} max={14} value={value.days} onChange={(v) => patch({ days: v ?? 14 })} /> ngày
            </span>
          </Space>
          <Space wrap size={16}>
            <span>
              Tối đa bài/ngày/kênh{' '}
              <InputNumber min={1} max={4} value={value.maxPerDayPerChannel} onChange={(v) => patch({ maxPerDayPerChannel: v ?? 2 })} />
            </span>
            <span>
              Video ngắn/ngày/kênh{' '}
              <InputNumber min={0} max={4} value={value.shortPerDayPerChannel} onChange={(v) => patch({ shortPerDayPerChannel: v ?? 1 })} />
            </span>
            <span>
              Video dài/tuần/kênh{' '}
              <InputNumber min={0} max={7} value={value.longPerWeekPerChannel} onChange={(v) => patch({ longPerWeekPerChannel: v ?? 2 })} />
            </span>
          </Space>
          <Typography.Text type="secondary">
            Khi duyệt phim ở bước 5 của Video Studio, Agent xếp các video của phim vào khung trống tiếp theo theo cài đặt này,
            sau các bài đã có lịch, rồi duyệt luôn. Video dài đi trước, video ngắn theo thứ tự.
          </Typography.Text>
          <Divider style={{ margin: '4px 0' }} />
          <Space wrap size={16} align="center">
            <Typography.Text strong>Viết tự động</Typography.Text>
            <Switch
              checked={value.autopilotEnabled}
              onChange={(on) => patch({ autopilotEnabled: on })}
              checkedChildren="Bật"
              unCheckedChildren="Tắt"
            />
            <span>
              Số bài/tuần{' '}
              <InputNumber
                min={1}
                max={7}
                disabled={!value.autopilotEnabled}
                value={value.autopilotPostsPerWeek}
                onChange={(v) => patch({ autopilotPostsPerWeek: v ?? 3 })}
              />
            </span>
            <Typography.Text type="secondary">
              Bật rồi bấm Lưu. Agent lấy bài YouTube/Fanpage có số xem để dựng khung, AI viết theo khung đó. Website đăng đúng giờ đã cài. Bài viết Fanpage đăng lúc 15:00. Video và YouTube không tự đăng.
            </Typography.Text>
            <Button
              loading={running}
              disabled={!value.saved || !value.autopilotEnabled}
              title={!value.saved || !value.autopilotEnabled ? 'Bật Viết tự động và bấm Lưu cài đặt trước' : undefined}
              onClick={() => void runNow()}
            >
              Viết ngay 1 bài
            </Button>
          </Space>
          {report ? (
            <Alert
              type="info"
              showIcon
              message="Lượt Viết tự động vừa chạy"
              description={<ul style={{ margin: 0, paddingLeft: 18 }}>{report.map((step, i) => <li key={i}>{step}</li>)}</ul>}
              closable
              onClose={() => setReport(null)}
            />
          ) : null}
          <Typography.Text type="secondary">
            Khi bật: mỗi ngày Agent viết nháp tối đa 1 bài từ tập kế tiếp của Series đang chạy, theo giọng người đăng và DNA
            nội dung của thương hiệu. Nháp nằm ở Bài viết chờ bạn duyệt; Agent dừng viết khi số nháp chờ duyệt bằng số bài/tuần.
            Bài bạn đã duyệt được Agent tự xếp giờ và duyệt giờ (Fanpage vào tab Đăng tay).
          </Typography.Text>
          <Divider style={{ margin: '4px 0' }} />
          <Space wrap size={16} align="center">
            <Typography.Text strong>Dọn file video đã đăng</Typography.Text>
            <span>
              Đăng xong giữ{' '}
              <InputNumber min={1} max={60} value={value.mediaKeepDaysPosted} onChange={(v) => patch({ mediaKeepDaysPosted: v ?? 3 })} /> ngày
            </span>
            <span>
              Đã huỷ giữ{' '}
              <InputNumber min={1} max={60} value={value.mediaKeepDaysCancelled} onChange={(v) => patch({ mediaKeepDaysCancelled: v ?? 7 })} /> ngày
            </span>
            <Button loading={cleaning} onClick={() => void cleanNow()}>Dọn ngay</Button>
          </Space>
          {storage ? (
            <Typography.Text type="secondary">
              File video đính bài trên máy chủ (mọi thương hiệu): {storage.files} file, {mb(storage.bytes)}. Dọn được ngay:{' '}
              {storage.cleanableFiles} file, {mb(storage.cleanableBytes)}. Đang chờ đăng hoặc chưa đến hạn: {storage.waitingFiles} file,{' '}
              {mb(storage.waitingBytes)}.{storage.lastCleanupNote ? ` Lần dọn gần nhất: ${storage.lastCleanupNote}` : ''}
            </Typography.Text>
          ) : null}
          <Typography.Text type="secondary">
            Máy chủ tự dọn mỗi 6 giờ: chỉ xoá file video, giữ bài, link đã đăng và lịch. Bài còn chờ đăng, đăng lỗi, hoặc Fanpage
            chưa bấm "Đã đăng" thì không xoá. Bấm "Lưu cài đặt" sau khi đổi số ngày.
          </Typography.Text>
        </Space>
      ) : null}
    </Card>
  );
}
