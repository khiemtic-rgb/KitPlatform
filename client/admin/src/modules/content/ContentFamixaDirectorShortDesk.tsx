import { Button, Checkbox, InputNumber, Segmented } from 'antd';
import type { ReactNode } from 'react';
import type { DirectorLaneMark } from './kit-video-director-nav';
import type { EditorialCut, EditorialCutMode } from './content-famixa-editorial-cut';

export type DirectorShortRow = {
  id: string;
  index: number;
  picture: DirectorLaneMark;
  video: DirectorLaneMark;
  voice: DirectorLaneMark;
  mark: '✓' | '●' | '○';
  status: string;
  openLabel: string;
  finalReady?: boolean;
  sourceReady?: boolean;
  inSec?: number;
  outSec?: number;
  keepSec?: number;
  capSec?: number;
};

export function ContentFamixaDirectorShortDesk({
  title,
  rows,
  allFinal,
  readyCount,
  allSourceReady,
  previewSec,
  editorial,
  assembleBusy,
  benchOpen,
  bench,
  onOpenShot,
  onBack,
  onEditorialMode,
  onEditorialMix,
  onEditorialTrim,
  onResetTrim,
  onPreviewWatch,
  onPreviewExport,
  onExportFinal,
  onExportSpeech,
}: {
  title: string;
  rows: DirectorShortRow[];
  allFinal?: boolean;
  readyCount?: number;
  allSourceReady?: boolean;
  previewSec?: number;
  editorial?: EditorialCut;
  assembleBusy?: boolean;
  benchOpen?: boolean;
  bench?: ReactNode;
  onOpenShot: (shotId: string) => void;
  onBack?: () => void;
  onEditorialMode?: (mode: EditorialCutMode) => void;
  onEditorialMix?: (patch: Partial<Pick<EditorialCut, 'room' | 'foley' | 'music' | 'loudnorm' | 'grade'>>) => void;
  onEditorialTrim?: (shotId: string, patch: { inSec?: number; outSec?: number }) => void;
  onResetTrim?: (shotId: string) => void;
  onPreviewWatch?: () => void;
  onPreviewExport?: () => void;
  onExportFinal?: () => void;
  onExportSpeech?: () => void;
}) {
  if (benchOpen && bench) {
    return (
      <section className="fx-desk" data-director-desk="shot">
        {onBack ? (
          <Button type="link" className="fx-dir-ws__back" onClick={onBack}>
            ← Short
          </Button>
        ) : null}
        {title ? <p className="fx-prod__kicker">{title}</p> : null}
        {bench}
      </section>
    );
  }

  const next = rows.find((row) => row.mark !== '✓') || rows[0];
  const ready = readyCount ?? rows.filter((row) => row.sourceReady).length;
  const total = rows.length;
  const canExport = allSourceReady ?? (total > 0 && ready === total);
  const cut = editorial;

  return (
    <section className="fx-desk" data-director-desk="short">
      <h2>Chỉnh dựng</h2>
      {title ? <p className="fx-prod__kicker">{title}</p> : null}
      <p className="fx-desk__lead">
        Cắt take đã có, ghép Short. Không tạo ảnh / video mới. 0 AI.
      </p>

      {total ? (
        <div className="fx-edit-cut" data-director-editorial="1">
          <h3>Cắt và xuất</h3>
          <p className="fx-desk__note">
            {ready}/{total} có video để dựng
            {typeof previewSec === 'number' ? ` · preview ~${previewSec.toFixed(1)}s` : ''}
            {canExport ? ' · đủ nguồn — được xuất' : ' · Xuất khi đủ video nguồn'}
            . FFmpeg only · 0 AI.
          </p>
          <div className="fx-edit-cut__row">
            <Segmented
              value={cut?.mode === 'FULL_TAKE' ? 'FULL_TAKE' : 'SPEECH_CUT'}
              onChange={(v) => onEditorialMode?.(v === 'FULL_TAKE' ? 'FULL_TAKE' : 'SPEECH_CUT')}
              options={[
                { label: 'Cắt theo thoại', value: 'SPEECH_CUT' },
                { label: 'Đủ take', value: 'FULL_TAKE' },
              ]}
            />
          </div>
          <div className="fx-edit-cut__row">
            <Checkbox checked={cut?.room !== false} onChange={(e) => onEditorialMix?.({ room: e.target.checked })}>
              Phòng
            </Checkbox>
            <Checkbox checked={cut?.foley !== false} onChange={(e) => onEditorialMix?.({ foley: e.target.checked })}>
              Foley
            </Checkbox>
            <Checkbox checked={cut?.music === true} onChange={(e) => onEditorialMix?.({ music: e.target.checked })}>
              Nhạc
            </Checkbox>
            <Checkbox checked={cut?.grade !== false} onChange={(e) => onEditorialMix?.({ grade: e.target.checked })}>
              Look
            </Checkbox>
            <Checkbox
              checked={cut?.loudnorm !== false}
              onChange={(e) => onEditorialMix?.({ loudnorm: e.target.checked })}
            >
              Chuẩn âm lượng
            </Checkbox>
          </div>
          {rows.map((row) => (
            <div key={`edit-${row.id}`} className={`fx-edit-cut__shot${row.sourceReady ? '' : ' is-pending'}`}>
              <span>
                {String(row.index + 1).padStart(2, '0')} {row.sourceReady ? '✓' : '○'}
                {row.sourceReady
                  ? typeof row.keepSec === 'number'
                    ? ` · Có video để dựng · giữ ${row.keepSec.toFixed(1)}s`
                    : ' · Có video để dựng'
                  : ' · Chưa có video để dựng'}
              </span>
              {row.sourceReady ? (
                <>
                  <InputNumber
                    size="small"
                    min={0}
                    max={Math.max(0, (row.capSec ?? 5) - 0.4)}
                    step={0.1}
                    value={row.inSec}
                    addonBefore="in"
                    onChange={(v) => onEditorialTrim?.(row.id, { inSec: Number(v) || 0 })}
                  />
                  <InputNumber
                    size="small"
                    min={0.4}
                    max={row.capSec ?? 5}
                    step={0.1}
                    value={row.outSec}
                    addonBefore="out"
                    onChange={(v) => onEditorialTrim?.(row.id, { outSec: Number(v) || 0 })}
                  />
                  <Button type="link" size="small" onClick={() => onResetTrim?.(row.id)}>
                    mặc định
                  </Button>
                </>
              ) : (
                <span>Chưa có lipsync / take — không vào preview</span>
              )}
            </div>
          ))}
          <div className="fx-desk__btns">
            <Button
              type="primary"
              disabled={!ready || assembleBusy}
              onClick={onPreviewWatch}
              data-director-watch-short="1"
            >
              Xem preview
            </Button>
            <Button disabled={!ready || assembleBusy} onClick={onPreviewExport} data-director-export-preview="1">
              Xuất preview
            </Button>
            <Button
              disabled={!canExport || assembleBusy}
              onClick={onExportFinal}
              data-director-export-short="1"
            >
              Xuất
            </Button>
            <Button
              disabled={!canExport || assembleBusy}
              onClick={onExportSpeech}
              data-director-export-speech="1"
            >
              Xuất cắt thoại
            </Button>
          </div>
          <p className="fx-desk__note">
            Preview = shot đã có video nguồn. Xuất = đủ nguồn → EP-edit.mp4. Hoàn thiện là cửa sản xuất, không phải cửa
            dựng. Watermark / HOME trong pixel nguồn — V1 không xoá. 0 AI.
          </p>
        </div>
      ) : null}

      {rows.length ? (
        <details className="fx-edit-cut__prod">
          <summary>Sản xuất shot · {rows.filter((row) => row.mark === '✓').length}/{total} Đạt</summary>
          <p className="fx-desk__note">
            Danh sách sản xuất. Không sang tab Hình / Video / Thoại.
          </p>
          <ul className="fx-wizard__sum" data-director-short-list="1">
            {rows.map((row) => (
              <li key={row.id}>
                <button type="button" className="fx-sw__shot-row" onClick={() => onOpenShot(row.id)}>
                  <strong>
                    {String(row.index + 1).padStart(2, '0')} {row.mark} {row.status}
                  </strong>
                  <span>
                    Hình {row.picture}
                    {'   '}Video {row.video}
                    {row.voice !== '—' ? `   Lời ${row.voice}` : ''}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <div className="fx-desk__btns">
            {next && !allFinal ? (
              <Button onClick={() => onOpenShot(next.id)}>{next.openLabel}</Button>
            ) : null}
          </div>
        </details>
      ) : (
        <p className="fx-desk__note">Chưa có Shot. Khóa chuyện trước.</p>
      )}
    </section>
  );
}
