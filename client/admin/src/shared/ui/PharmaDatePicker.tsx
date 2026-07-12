import { DatePicker, Select, Space } from 'antd';
import type { DatePickerProps } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import customParseFormat from 'dayjs/plugin/customParseFormat';
import type { CSSProperties, MouseEvent } from 'react';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

dayjs.extend(customParseFormat);

const POPUP_Z_INDEX = 2000;
/** Select năm/tháng phải cao hơn panel DatePicker, nếu không list bị che / “bấm không ra”. */
const SELECT_POPUP_Z_INDEX = POPUP_Z_INDEX + 50;

function toDayjs(value?: string): Dayjs | null {
  if (!value) return null;
  const normalized = value.length >= 10 ? value.slice(0, 10) : value;
  const parsed = dayjs(normalized, 'YYYY-MM-DD', true);
  if (parsed.isValid()) return parsed;
  const loose = dayjs(value);
  return loose.isValid() ? loose : null;
}

function stopBubble(event: MouseEvent) {
  event.stopPropagation();
}

interface PharmaDatePickerProps extends Omit<DatePickerProps, 'value' | 'onChange'> {
  value?: string;
  onChange?: (value: string) => void;
  style?: CSSProperties;
  /** Dùng trong bảng GRN — size nhỏ, chặn event của Table. */
  inTable?: boolean;
  /**
   * Hiện Select năm/tháng trên panel (ngày sinh 1950… không phải bấm «« nhiều lần).
   * Mặc định bật.
   */
  quickYear?: boolean;
  /** Năm đầu trong droplist (mặc định 1920). */
  yearFrom?: number;
  /** Năm cuối trong droplist (mặc định năm hiện tại + 5). */
  yearTo?: number;
}

/** DatePicker (dropdown tháng/năm), giá trị YYYY-MM-DD cho API. */
export function PharmaDatePicker({
  value,
  onChange,
  format = 'DD/MM/YYYY',
  placeholder,
  style,
  inTable = false,
  size,
  quickYear = true,
  yearFrom = 1920,
  yearTo,
  allowClear = true,
  ...rest
}: PharmaDatePickerProps) {
  const { t } = useTranslation('common', { keyPrefix: 'datePicker' });
  const parsed = toDayjs(value);
  const endYear = yearTo ?? dayjs().year() + 5;
  const [pickerValue, setPickerValue] = useState<Dayjs>(
    () => parsed ?? dayjs().year(Math.min(Math.max(1990, yearFrom), endYear)),
  );

  useEffect(() => {
    if (parsed) setPickerValue(parsed);
  }, [value]);

  const yearOptions = useMemo(() => {
    const opts: { value: number; label: string }[] = [];
    for (let y = endYear; y >= yearFrom; y -= 1) {
      opts.push({ value: y, label: String(y) });
    }
    return opts;
  }, [yearFrom, endYear]);

  const monthOptions = useMemo(
    () =>
      Array.from({ length: 12 }, (_, month) => ({
        value: month,
        label: t('monthN', { n: month + 1, defaultValue: `Thg ${month + 1}` }),
      })),
    [t],
  );

  return (
    <div
      className="pharma-date-picker-wrap"
      onClick={inTable ? stopBubble : undefined}
      onMouseDown={inTable ? stopBubble : undefined}
    >
      <DatePicker
        {...rest}
        size={size ?? (inTable ? 'small' : 'middle')}
        value={parsed}
        format={format}
        placeholder={placeholder ?? t('pickDate')}
        allowClear={allowClear}
        style={{ width: '100%', ...style }}
        getPopupContainer={() => document.body}
        popupStyle={{ zIndex: POPUP_Z_INDEX }}
        styles={{ popup: { root: { zIndex: POPUP_Z_INDEX } } }}
        pickerValue={quickYear ? pickerValue : undefined}
        onPickerValueChange={quickYear ? (next) => setPickerValue(next) : undefined}
        panelRender={
          quickYear
            ? (panel) => (
                <div
                  onMouseDown={stopBubble}
                  onClick={stopBubble}
                >
                  <Space
                    size={8}
                    style={{
                      display: 'flex',
                      justifyContent: 'center',
                      padding: '8px 12px 4px',
                      width: '100%',
                    }}
                  >
                    <Select
                      size="small"
                      showSearch
                      optionFilterProp="label"
                      placeholder={t('year', { defaultValue: 'Năm' })}
                      value={pickerValue.year()}
                      options={yearOptions}
                      style={{ width: 100 }}
                      listHeight={280}
                      popupMatchSelectWidth={false}
                      onChange={(y) => setPickerValue(pickerValue.year(y))}
                      getPopupContainer={() => document.body}
                      styles={{ popup: { root: { zIndex: SELECT_POPUP_Z_INDEX } } }}
                      onMouseDown={stopBubble}
                      onClick={stopBubble}
                    />
                    <Select
                      size="small"
                      value={pickerValue.month()}
                      options={monthOptions}
                      style={{ width: 92 }}
                      listHeight={280}
                      onChange={(m) => setPickerValue(pickerValue.month(m))}
                      getPopupContainer={() => document.body}
                      styles={{ popup: { root: { zIndex: SELECT_POPUP_Z_INDEX } } }}
                      onMouseDown={stopBubble}
                      onClick={stopBubble}
                    />
                  </Space>
                  {panel}
                </div>
              )
            : undefined
        }
        onChange={(date) => onChange?.(date ? date.format('YYYY-MM-DD') : '')}
      />
    </div>
  );
}

/** HSD phiếu nhập: chọn tháng/năm, lưu ngày cuối tháng. */
export function PharmaExpiryPicker({
  value,
  onChange,
  style,
  inTable = false,
}: {
  value?: string;
  onChange?: (value: string) => void;
  style?: CSSProperties;
  inTable?: boolean;
}) {
  const { t } = useTranslation('common', { keyPrefix: 'datePicker' });
  return (
    <div
      className="pharma-date-picker-wrap"
      onClick={inTable ? stopBubble : undefined}
      onMouseDown={inTable ? stopBubble : undefined}
    >
      <DatePicker
        picker="month"
        size={inTable ? 'small' : 'middle'}
        format="MM/YYYY"
        placeholder={t('monthYear')}
        allowClear
        value={toDayjs(value)}
        style={{ width: '100%', ...style }}
        getPopupContainer={() => document.body}
        popupStyle={{ zIndex: POPUP_Z_INDEX }}
        styles={{ popup: { root: { zIndex: POPUP_Z_INDEX } } }}
        onChange={(date) => {
          if (!date) {
            onChange?.('');
            return;
          }
          onChange?.(date.endOf('month').format('YYYY-MM-DD'));
        }}
      />
    </div>
  );
}
