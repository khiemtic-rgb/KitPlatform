import { DatePicker } from 'antd';
import type { DatePickerProps } from 'antd';
import dayjs, { type Dayjs } from 'dayjs';
import customParseFormat from 'dayjs/plugin/customParseFormat';
import type { CSSProperties, MouseEvent } from 'react';

dayjs.extend(customParseFormat);

const POPUP_Z_INDEX = 2000;

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
}

/** DatePicker tiếng Việt (dropdown tháng/năm), giá trị YYYY-MM-DD cho API. */
export function PharmaDatePicker({
  value,
  onChange,
  format = 'DD/MM/YYYY',
  placeholder = 'Chọn ngày',
  style,
  inTable = false,
  size,
  ...rest
}: PharmaDatePickerProps) {
  return (
    <div
      className="pharma-date-picker-wrap"
      onClick={inTable ? stopBubble : undefined}
      onMouseDown={inTable ? stopBubble : undefined}
    >
      <DatePicker
        {...rest}
        size={size ?? (inTable ? 'small' : 'middle')}
        value={toDayjs(value)}
        format={format}
        placeholder={placeholder}
        allowClear
        style={{ width: '100%', ...style }}
        getPopupContainer={() => document.body}
        popupStyle={{ zIndex: POPUP_Z_INDEX }}
        styles={{ popup: { root: { zIndex: POPUP_Z_INDEX } } }}
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
        placeholder="Tháng/Năm"
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
