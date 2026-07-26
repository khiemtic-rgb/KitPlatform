import { useState, type ReactNode } from 'react';
import { Button, Divider, Input, Select, Space } from 'antd';
import type { SelectProps } from 'antd';
import { PlusOutlined } from '@ant-design/icons';

export type QuickAddOption = { value: string | number; label: ReactNode };

type Props = Omit<SelectProps, 'options' | 'popupRender' | 'dropdownRender'> & {
  options: QuickAddOption[];
  /** Placeholder trong ô nhập tên mới (cuối dropdown). */
  addPlaceholder: string;
  /** Nhãn nút thêm. */
  addLabel: string;
  /** Ẩn hàng thêm nhanh (vd thiếu quyền catalog.write). */
  canAdd?: boolean;
  /** Tạo bản ghi mới; trả về option để chọn ngay. */
  onQuickAdd: (name: string) => Promise<QuickAddOption | null>;
};

/** Select + hàng «Thêm nhanh» trong dropdown. */
export function CatalogQuickAddSelect({
  options,
  addPlaceholder,
  addLabel,
  canAdd = true,
  onQuickAdd,
  ...selectProps
}: Props) {
  const [draft, setDraft] = useState('');
  const [adding, setAdding] = useState(false);

  const handleAdd = async () => {
    const name = draft.trim();
    if (!name || adding) return;
    setAdding(true);
    try {
      const created = await onQuickAdd(name);
      if (created) {
        setDraft('');
        selectProps.onChange?.(created.value, created as never);
      }
    } finally {
      setAdding(false);
    }
  };

  return (
    <Select
      showSearch
      optionFilterProp="label"
      {...selectProps}
      options={options}
      popupRender={(menu) => (
        <>
          {menu}
          {canAdd ? (
            <>
              <Divider style={{ margin: '8px 0' }} />
              {/*
                Không preventDefault trên wrapper — sẽ chặn focus Input.
                stopPropagation keyDown: Select không nuốt phím khi đang gõ ô thêm.
              */}
              <Space.Compact style={{ padding: '0 8px 8px', width: '100%' }}>
                <Input
                  size="small"
                  value={draft}
                  placeholder={addPlaceholder}
                  onChange={(e) => setDraft(e.target.value)}
                  onMouseDown={(e) => e.stopPropagation()}
                  onKeyDown={(e) => e.stopPropagation()}
                  onClick={(e) => e.stopPropagation()}
                  onPressEnter={(e) => {
                    e.preventDefault();
                    e.stopPropagation();
                    void handleAdd();
                  }}
                  disabled={adding}
                />
                <Button
                  type="primary"
                  size="small"
                  icon={<PlusOutlined />}
                  loading={adding}
                  disabled={!draft.trim()}
                  onMouseDown={(e) => e.preventDefault()}
                  onClick={() => void handleAdd()}
                >
                  {addLabel}
                </Button>
              </Space.Compact>
            </>
          ) : null}
        </>
      )}
    />
  );
}

/** Sinh mã ngắn từ tên (bỏ dấu) + hậu tố tránh trùng. */
export function suggestCatalogCode(name: string, prefix: string, maxLen = 24): string {
  const slug = name
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/gi, 'd')
    .toUpperCase()
    .replace(/[^A-Z0-9]+/g, '')
    .slice(0, 12);
  const base = slug || 'NEW';
  const suffix = Date.now().toString(36).slice(-4).toUpperCase();
  return `${prefix}${base}${suffix}`.slice(0, maxLen);
}
