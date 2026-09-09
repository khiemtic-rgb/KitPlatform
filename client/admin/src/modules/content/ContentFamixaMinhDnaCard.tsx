import { useEffect, useState } from 'react';
import { Alert, Button, Card, Collapse, Input, Space, Tag, Typography, message } from 'antd';
import { fetchFamixaCharacter, putFamixaCharacterCanon, type FamixaCharacterRow } from '@/shared/api/content.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { parseFamixaCanon, serializeFamixaCanon, type FamixaCharacterCanon } from './content-famixa-character-memory';
import { FAMIXA_VISUAL_STYLE_V1, styleApproved, styleReadyForMaster } from './content-famixa-visual-style';
import {
  CHAR_001_MINH_VISUAL_DNA_V1,
  dnaApproved,
  dnaReadyForMaster,
  mergeMinhVisualDnaDraft,
} from './content-famixa-minh-visual-dna';
import { lookStatusVi } from './content-famixa-look-status';

const GROUPS: { title: string; hint: string; fields: { key: keyof typeof CHAR_001_MINH_VISUAL_DNA_V1; label: string }[] }[] = [
  {
    title: 'Minh là ai',
    hint: 'Cậu bé đời thường, dễ tổn thương — không hot boy, không trẻ quảng cáo.',
    fields: [
      { key: 'whoVisually', label: 'Tổng thể' },
      { key: 'identityPrinciple', label: 'Vùng hình (không thật quá, không hoạt hình)' },
    ],
  },
  {
    title: 'Khuôn mặt',
    hint: 'Mặt là thứ nhận ra Minh trước nhất. Diễn bằng mắt.',
    fields: [
      { key: 'headShape', label: 'Mặt' },
      { key: 'eyeShape', label: 'Mắt' },
      { key: 'eyebrows', label: 'Lông mày' },
      { key: 'noseShape', label: 'Mũi' },
      { key: 'mouthShape', label: 'Miệng' },
    ],
  },
  {
    title: 'Tóc và da',
    hint: 'Kiểu tóc là “chữ ký”. Máy không được tự đổi.',
    fields: [
      { key: 'hairLock', label: 'Tóc (không đổi kiểu)' },
      { key: 'skin', label: 'Da' },
    ],
  },
  {
    title: 'Dáng người',
    hint: 'Đúng trẻ 11 tuổi. Không thân người lớn gắn mặt trẻ.',
    fields: [
      { key: 'bodyProportion', label: 'Tỷ lệ' },
      { key: 'silhouette', label: 'Bóng dáng' },
      { key: 'posture', label: 'Tư thế' },
    ],
  },
  {
    title: 'Cảm xúc và quần áo',
    hint: 'Giữ cảm xúc trước khi lộ. Đồ mặc chỉ là nền, không thay mặt.',
    fields: [
      { key: 'emotionalSignature', label: 'Cách lộ cảm xúc' },
      { key: 'wardrobeBaseline', label: 'Đồ mặc gốc' },
      { key: 'colorPersonality', label: 'Màu gần Minh' },
    ],
  },
];

export function ContentFamixaMinhDnaCard() {
  const [row, setRow] = useState<FamixaCharacterRow>();
  const [canon, setCanon] = useState<FamixaCharacterCanon>();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string>();
  const [okNote, setOkNote] = useState<string>();

  const load = () => {
    setBusy(true);
    void fetchFamixaCharacter('CHAR-001')
      .then((next) => {
        const parsed = parseFamixaCanon(next.canon);
        const merged = parseFamixaCanon(
          mergeMinhVisualDnaDraft({
            identity: parsed.identity,
            visualDna: parsed.visualDna,
            famixaVisualStyle: parsed.famixaVisualStyle ?? {
              documentId: FAMIXA_VISUAL_STYLE_V1.documentId,
              status: 'DRAFT',
              summary: FAMIXA_VISUAL_STYLE_V1.summary,
            },
            references: parsed.references,
            voiceDna: parsed.voiceDna,
            workspace: parsed.workspace,
          }),
        );
        if (!merged.famixaVisualStyle?.documentId) {
          merged.famixaVisualStyle = { ...FAMIXA_VISUAL_STYLE_V1, status: merged.famixaVisualStyle?.status || 'DRAFT' };
        }
        setRow(next);
        setCanon(merged);
        setError(undefined);
      })
      .catch((e) => setError(apiErrorMessage(e, 'Không đọc được hồ sơ Minh.')))
      .finally(() => setBusy(false));
  };

  useEffect(() => {
    load();
  }, []);

  const save = async (next: FamixaCharacterCanon, done: string) => {
    setBusy(true);
    try {
      const saved = await putFamixaCharacterCanon('CHAR-001', serializeFamixaCanon(next));
      setRow(saved);
      setCanon(parseFamixaCanon(saved.canon));
      setError(undefined);
      setOkNote(done);
      message.success(done);
      window.dispatchEvent(new Event('famixa-look-changed'));
    } catch (e) {
      setError(apiErrorMessage(e, 'Không lưu được mô tả Minh.'));
    } finally {
      setBusy(false);
    }
  };

  const patchDna = (key: string, value: string) => {
    if (!canon) return;
    setCanon({ ...canon, visualDna: { ...canon.visualDna, [key]: value } });
  };

  const style = canon?.famixaVisualStyle;
  const dna = canon?.visualDna;
  const styleOk = styleReadyForMaster(style);
  const dnaOk = dnaReadyForMaster(dna);
  const dnaChip = lookStatusVi(dna?.status);
  const styleChip = lookStatusVi(style?.status);

  return (
    <Card className="fx-look__card" size="small" title="Bước 2 · Minh trông như thế nào (11 tuổi)">
      <p className="fx-look__help">
        Đây là <b>giấy mô tả hình ảnh</b> của Minh — chưa phải ảnh. Đọc để hình dung một cậu bé mình từng gặp: thông
        minh, hơi hướng nội, dễ tổn thương. Director duyệt mô tả này xong mới được vẽ ảnh hộ chiếu.
      </p>
      <Space wrap style={{ marginBottom: 8 }}>
        <Tag color={dnaChip.color}>Mô tả Minh · {dnaChip.label}</Tag>
        <Tag color={styleChip.color}>Luật hình · {styleChip.label}</Tag>
        <Tag>11 tuổi</Tag>
        <Tag color={row?.lifecycle === 'locked' ? 'green' : undefined}>
          Nhân vật · {row?.lifecycle === 'locked' ? 'đã khóa' : 'chưa khóa'}
        </Tag>
      </Space>
      {error ? <Alert type="warning" showIcon message={error} style={{ marginBottom: 8 }} /> : null}
      {okNote ? <Alert type="success" showIcon message={okNote} style={{ marginBottom: 8 }} /> : null}
      <Alert
        type={dnaApproved(dna) ? 'success' : dnaReadyForMaster(dna) ? 'warning' : 'info'}
        showIcon
        style={{ marginBottom: 8 }}
        message={dnaApproved(dna) ? 'Mô tả đã duyệt. Xuống bước 3 để vẽ ảnh hộ chiếu.' : dnaReadyForMaster(dna) ? 'Đã gửi. Bấm «Director duyệt mô tả» để chốt.' : 'Bạn đang ở bước này.'}
        description={dnaChip.hint}
      />
      <Collapse
        size="small"
        defaultActiveKey={['0']}
        items={GROUPS.map((g, i) => ({
          key: String(i),
          label: `${g.title} — ${g.hint}`,
          children: (
            <div className="fx-look__grid">
              {g.fields.map((f) => (
                <div key={f.key}>
                  <Typography.Text strong>{f.label}</Typography.Text>
                  <Input.TextArea rows={3} value={String(dna?.[f.key] ?? '')} onChange={(e) => patchDna(String(f.key), e.target.value)} />
                </div>
              ))}
            </div>
          ),
        }))}
      />
      <Collapse
        size="small"
        style={{ marginTop: 8 }}
        items={[
          {
            key: 'qa',
            label: 'Khi có ảnh rồi, kiểm tra gì? (chưa vẽ)',
            children: (
              <ul style={{ margin: 0, paddingLeft: 18, lineHeight: 1.6 }}>
                <li>Cùng Minh khi nhìn thẳng / nghiêng, vui / buồn, đồ trường / đồ nhà, sáng / tối.</li>
                <li>Cùng Minh khi cận mặt và khi thấy cả người.</li>
                <li>Sau này Minh 16 tuổi vẫn nhận ra là Minh lớn lên — không phải bé khác.</li>
              </ul>
            ),
          },
        ]}
      />
      <Space wrap style={{ marginTop: 8 }}>
        <Button size="small" loading={busy} onClick={load}>
          Tải lại
        </Button>
        <Button
          size="small"
          loading={busy}
          disabled={!canon}
          onClick={() =>
            void save(
              {
                ...canon!,
                famixaVisualStyle: { ...FAMIXA_VISUAL_STYLE_V1, status: style?.status || 'DRAFT' },
                visualDna: { ...dna, status: 'DRAFT' },
              },
              'Đã lưu bản nháp. Chưa duyệt.',
            )
          }
        >
          Lưu bản nháp
        </Button>
        <Button
          size="small"
          loading={busy}
          disabled={!canon}
          onClick={() =>
            void save(
              {
                ...canon!,
                famixaVisualStyle: { ...FAMIXA_VISUAL_STYLE_V1, ...style, status: 'REVIEW' },
                visualDna: { ...dna, status: 'REVIEW' },
              },
              dnaReadyForMaster(dna)
                ? 'Mô tả đã ở «Chờ duyệt». Bấm «Director duyệt mô tả» (nút bên cạnh) để chốt chữ.'
                : 'Đã gửi duyệt. Tiếp: bấm «Director duyệt mô tả».',
            )
          }
        >
          Gửi duyệt
        </Button>
        <Button
          size="small"
          type="primary"
          loading={busy}
          disabled={!canon || !styleOk || !dnaOk}
          onClick={() =>
            void save(
              {
                ...canon!,
                famixaVisualStyle: { ...FAMIXA_VISUAL_STYLE_V1, ...style, status: 'APPROVED' },
                visualDna: { ...dna, status: 'APPROVED' },
              },
              'Đã duyệt mô tả Minh. Xuống bước 3 — ghi chỗ sai rồi Vẽ lại. Đừng chọn ảnh thử máy.',
            )
          }
        >
          Director duyệt mô tả
        </Button>
      </Space>
      <Typography.Paragraph type="secondary" style={{ marginTop: 8, marginBottom: 0 }}>
        Duyệt ở đây chỉ chốt chữ. Không khóa nhân vật, không vẽ ảnh, không chạy máy quay.
        {styleApproved(style) && dnaApproved(dna) ? ' Mô tả đã duyệt — bước sau mới chụp ảnh hộ chiếu.' : ''}
      </Typography.Paragraph>
    </Card>
  );
}
