import { useCallback, useEffect, useRef, useState } from 'react';
import { Select, Spin } from 'antd';
import { fetchProduct, fetchProducts } from '@/shared/api/catalog.api';
import type { ProductListItem } from '@/shared/api/catalog.types';

type Option = { value: string; label: string };

type Props = {
  value?: string;
  onChange?: (value: string | undefined) => void;
  /** Gọi sau khi đổi SP (vd. clear ĐVT) — không thay Form.Item onChange. */
  afterChange?: (value: string | undefined) => void;
  disabled?: boolean;
  placeholder?: string;
  style?: React.CSSProperties;
  /** Seed options (vd. SP đã chọn từ PO) để hiện label ngay. */
  seedProducts?: ProductListItem[];
  className?: string;
};

function toOption(p: { id: string; productCode: string; productName: string }): Option {
  return { value: p.id, label: `${p.productCode} — ${p.productName}` };
}

/**
 * Select SP tìm trên toàn danh mục (API search) — không giới hạn 200 dòng load sẵn.
 */
export function ProductSearchSelect({
  value,
  onChange,
  afterChange,
  disabled,
  placeholder,
  style,
  seedProducts,
  className,
}: Props) {
  const [options, setOptions] = useState<Option[]>([]);
  const [loading, setLoading] = useState(false);
  const timer = useRef<number | null>(null);
  const known = useRef<Map<string, Option>>(new Map());

  const mergeOptions = useCallback((items: Option[]) => {
    for (const o of items) known.current.set(o.value, o);
    setOptions((prev) => {
      const map = new Map<string, Option>();
      for (const o of prev) map.set(o.value, o);
      for (const o of items) map.set(o.value, o);
      if (value && known.current.has(value)) map.set(value, known.current.get(value)!);
      return [...map.values()];
    });
  }, [value]);

  useEffect(() => {
    if (seedProducts?.length) {
      mergeOptions(seedProducts.map(toOption));
    }
  }, [seedProducts, mergeOptions]);

  useEffect(() => {
    if (!value) return;
    if (known.current.has(value)) {
      mergeOptions([known.current.get(value)!]);
      return;
    }
    let cancelled = false;
    void fetchProduct(value)
      .then((p) => {
        if (cancelled) return;
        mergeOptions([toOption(p)]);
      })
      .catch(() => undefined);
    return () => {
      cancelled = true;
    };
  }, [value, mergeOptions]);

  const runSearch = useCallback(
    (q: string) => {
      if (timer.current) window.clearTimeout(timer.current);
      timer.current = window.setTimeout(() => {
        void (async () => {
          setLoading(true);
          try {
            const result = await fetchProducts({
              search: q.trim() || undefined,
              status: 1,
              page: 1,
              pageSize: 80,
            });
            mergeOptions(result.items.map(toOption));
            // Replace visible list with search hits (+ selected)
            const hits = result.items.map(toOption);
            if (value && known.current.has(value) && !hits.some((h) => h.value === value)) {
              setOptions([known.current.get(value)!, ...hits]);
            } else {
              setOptions(hits);
            }
          } finally {
            setLoading(false);
          }
        })();
      }, 280);
    },
    [mergeOptions, value],
  );

  useEffect(() => {
    // Prefetch trang đầu để mở dropdown có sẵn vài chục SP
    runSearch('');
    return () => {
      if (timer.current) window.clearTimeout(timer.current);
    };
  }, [runSearch]);

  return (
    <Select
      showSearch
      allowClear
      value={value || undefined}
      onChange={(v) => {
        onChange?.(v);
        afterChange?.(v);
      }}
      disabled={disabled}
      placeholder={placeholder ?? 'Gõ mã / tên SP để tìm…'}
      style={style}
      className={className}
      filterOption={false}
      onSearch={runSearch}
      onDropdownVisibleChange={(open) => {
        if (open && options.length === 0) runSearch('');
      }}
      options={options}
      notFoundContent={loading ? <Spin size="small" /> : 'Không thấy SP — thử từ khóa khác'}
      optionFilterProp="label"
    />
  );
}
