import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import {
  Alert,
  App,
  Avatar,
  Button,
  Drawer,
  Divider,
  Form,
  Input,
  InputNumber,
  Popconfirm,
  Select,
  Space,
  Spin,
  Tabs,
  Tag,
  Typography,
  Upload,
} from 'antd';
import type { FormInstance } from 'antd/es/form';
import type { UploadRequestOption } from 'rc-upload/lib/interface';
import { DeleteOutlined, StarFilled, StarOutlined, UploadOutlined } from '@ant-design/icons';
import { isAxiosError } from 'axios';
import {
  createProduct,
  checkSimilarProductNames,
  checkBarcode,
  deleteProduct,
  fetchBrandLookups,
  fetchCategoryLookups,
  fetchIngredientLookups,
  fetchNextProductCode,
  syncProductCommercial,
  syncProductIngredients,
  syncProductUnits,
  updateProduct,
  type BarcodeCheckResult,
  type ProductCommercialPayload,
  type ProductIngredientPayload,
  type ProductUnitPayload,
  type SimilarProductMatch,
} from '@/shared/api/catalog.api';
import { apiErrorMessage } from '@/shared/api/api-error';
import { withUploadAuth } from '@/shared/utils/upload-url';
import { uploadImage } from '@/shared/api/files.api';
import type { LookupItem, ProductDetail } from '@/shared/api/catalog.types';
import type { ProductFormNationalPrefill } from '@/shared/api/national-drug.types';
import { SALE_UNIT_OPTIONS } from '@/shared/api/catalog.types';
import { catalogT } from '@/shared/i18n';
import { useCatalogEnums } from '@/shared/i18n/use-catalog-enums';
import { formatDisplayMoney, moneyInputNumberPropsAllowZeroSuffix, moneyInputNumberStyle } from '@/shared/utils/money';

type TabKey = 'general' | 'details' | 'ingredients';

const emptyCommercial = (): ProductCommercialPayload => ({
  barcodes: [],
  prices: [],
  images: [],
});

function readTextField(form: FormInstance, name: string): string | undefined {
  const value = form.getFieldValue(name);
  if (value == null) return undefined;
  const text = String(value).trim();
  return text || undefined;
}

function toCommercialPayload(product: ProductDetail): ProductCommercialPayload {
  return {
    barcodes: product.barcodes.map((b) => ({
      barcode: b.barcode,
      isPrimary: b.isPrimary,
      barcodeType: b.barcodeType,
    })),
    prices: product.prices.map((p) => ({
      priceType: p.priceType,
      price: p.price,
      productUnitId: p.productUnitId,
    })),
    images: product.images.map((img, index) => ({
      imageUrl: img.imageUrl,
      isPrimary: img.isPrimary,
      sortOrder: index,
    })),
  };
}

function toUnitsPayload(product: ProductDetail): ProductUnitPayload[] {
  return product.units.map((u) => ({
    id: u.id,
    unitName: u.unitName,
    conversionFactor: u.conversionFactor,
    isBaseUnit: u.isBaseUnit,
    isSaleUnit: u.isSaleUnit,
  }));
}

type IngredientRow = ProductIngredientPayload & {
  ingredientName: string;
  ingredientCode: string;
};

function toIngredientRows(product: ProductDetail): IngredientRow[] {
  return (product.ingredients ?? []).map((ing) => ({
    ingredientId: ing.ingredientId,
    ingredientName: ing.ingredientName,
    ingredientCode: ing.ingredientCode,
    strengthValue: ing.strengthValue,
    strengthUnit: ing.strengthUnit,
  }));
}

function formatGenericFromIngredients(rows: IngredientRow[]): string {
  const formatLine = (row: IngredientRow) => {
    const name = row.ingredientName || row.ingredientCode;
    if (!name) return '';
    if (row.strengthValue != null && row.strengthUnit) {
      return `${name} ${row.strengthValue} ${row.strengthUnit}`.trim();
    }
    if (row.strengthValue != null) return `${name} ${row.strengthValue}`.trim();
    return name;
  };
  return rows.map(formatLine).filter(Boolean).join(' + ');
}

type Props = {
  open: boolean;
  editing: ProductDetail | null;
  nationalPrefill?: ProductFormNationalPrefill | null;
  onClose: () => void;
  onCreated: (product: ProductDetail) => void;
  onUpdated: (product?: ProductDetail) => void;
};

export function ProductFormDrawer({ open, editing, nationalPrefill, onClose, onCreated, onUpdated }: Props) {
  const { t } = useTranslation('catalog');
  const { t: tc } = useTranslation('common');
  const {
    drugTypeOptions,
    productStatusOptions,
    barcodeTypeOptions,
    priceTypeOptions,
    priceTypeLabel,
  } = useCatalogEnums();
  const { message: msg, modal } = App.useApp();
  const [form] = Form.useForm();
  const linkedNationalDrugId = Form.useWatch('nationalDrugId', form);
  const linkedNationalReg = Form.useWatch('nationalRegistrationNumber', form);
  const [saving, setSaving] = useState(false);
  const [commercialSaving, setCommercialSaving] = useState(false);
  const [uploading, setUploading] = useState(false);
  const [categories, setCategories] = useState<LookupItem[]>([]);
  const [brands, setBrands] = useState<LookupItem[]>([]);
  const [draftBarcode, setDraftBarcode] = useState('');
  const [draftBarcodeType, setDraftBarcodeType] = useState(1);
  const [draftImageUrl, setDraftImageUrl] = useState('');
  const [draftPriceType, setDraftPriceType] = useState(1);
  const [draftPrice, setDraftPrice] = useState<number | undefined>();
  const [draftProductUnitId, setDraftProductUnitId] = useState<string>('');
  const [draftUnitName, setDraftUnitName] = useState(() => catalogT()('shared.defaultPackUnit'));
  const [draftConversionFactor, setDraftConversionFactor] = useState<number | undefined>(10);
  const [draftUnitIsSale, setDraftUnitIsSale] = useState(true);
  const [draftIngredientId, setDraftIngredientId] = useState('');
  const [draftStrengthValue, setDraftStrengthValue] = useState<number | undefined>();
  const [draftStrengthUnit, setDraftStrengthUnit] = useState('mg');
  const [ingredientOptions, setIngredientOptions] = useState<LookupItem[]>([]);
  const [unitsSaving, setUnitsSaving] = useState(false);
  const [ingredientsSaving, setIngredientsSaving] = useState(false);
  const [generalDirty, setGeneralDirty] = useState(false);
  const [activeTab, setActiveTab] = useState<TabKey>('general');
  const [showCreatedHint, setShowCreatedHint] = useState(false);
  const [loadingCode, setLoadingCode] = useState(false);
  const [similarMatches, setSimilarMatches] = useState<SimilarProductMatch[]>([]);
  const [sessionProduct, setSessionProduct] = useState<ProductDetail | null>(null);
  const [commercial, setCommercial] = useState<ProductCommercialPayload>(emptyCommercial);
  const [units, setUnits] = useState<ProductUnitPayload[]>([]);
  const [ingredientRows, setIngredientRows] = useState<IngredientRow[]>([]);
  const productRef = useRef<ProductDetail | null>(null);
  const commercialRef = useRef<ProductCommercialPayload>(emptyCommercial());
  const unitsRef = useRef<ProductUnitPayload[]>([]);
  const ingredientsRef = useRef<IngredientRow[]>([]);
  const persistChainRef = useRef<Promise<ProductDetail | undefined>>(Promise.resolve(undefined));
  const unitsPersistChainRef = useRef<Promise<ProductDetail | undefined>>(Promise.resolve(undefined));
  const ingredientsPersistChainRef = useRef<Promise<ProductDetail | undefined>>(Promise.resolve(undefined));
  const baselineGeneralRef = useRef('');

  const displayProduct = sessionProduct ?? editing;
  const hasPersistedId = displayProduct !== null;

  const syncCommercialFromProduct = useCallback((product: ProductDetail | null) => {
    const next = product ? toCommercialPayload(product) : emptyCommercial();
    commercialRef.current = next;
    setCommercial(next);
  }, []);

  const syncUnitsFromProduct = useCallback((product: ProductDetail | null) => {
    const next = product ? toUnitsPayload(product) : [];
    unitsRef.current = next;
    setUnits(next);
  }, []);

  const syncIngredientsFromProduct = useCallback((product: ProductDetail | null) => {
    const next = product ? toIngredientRows(product) : [];
    ingredientsRef.current = next;
    setIngredientRows(next);
  }, []);

  useEffect(() => {
    productRef.current = displayProduct;
    if (!commercialSaving) syncCommercialFromProduct(displayProduct);
    if (!unitsSaving) syncUnitsFromProduct(displayProduct);
    if (!ingredientsSaving) syncIngredientsFromProduct(displayProduct);
  }, [
    displayProduct,
    commercialSaving,
    unitsSaving,
    ingredientsSaving,
    syncCommercialFromProduct,
    syncUnitsFromProduct,
    syncIngredientsFromProduct,
  ]);

  const currentProduct = () => productRef.current ?? displayProduct;

  const unitOptions = useMemo(() => {
    const product = displayProduct;
    const source = product?.units?.length ? product.units : [];
    return source.map((u) => ({ value: u.id, label: u.unitName }));
  }, [displayProduct]);

  const defaultUnitId = unitOptions[0]?.value ?? '';

  const resetDrafts = () => {
    setDraftBarcode('');
    setDraftBarcodeType(1);
    setDraftImageUrl('');
    setDraftPriceType(1);
    setDraftPrice(undefined);
    setDraftProductUnitId('');
    setDraftUnitName(catalogT()('shared.defaultPackUnit'));
    setDraftConversionFactor(10);
    setDraftUnitIsSale(true);
    setDraftIngredientId('');
    setDraftStrengthValue(undefined);
    setDraftStrengthUnit('mg');
    setActiveTab('general');
    setShowCreatedHint(false);
    setSimilarMatches([]);
    setGeneralDirty(false);
    baselineGeneralRef.current = '';
    setSessionProduct(null);
    commercialRef.current = emptyCommercial();
    setCommercial(emptyCommercial());
    unitsRef.current = [];
    setUnits([]);
    ingredientsRef.current = [];
    setIngredientRows([]);
    persistChainRef.current = Promise.resolve(undefined);
    unitsPersistChainRef.current = Promise.resolve(undefined);
    ingredientsPersistChainRef.current = Promise.resolve(undefined);
  };

  const loadEditingProduct = (product: ProductDetail) => {
    form.setFieldsValue({
      productCode: product.productCode,
      productName: product.productName,
      genericName: product.genericName,
      drugType: product.drugType,
      categoryId: product.categoryId,
      brandId: product.brandId,
      description: product.description,
      nationalDrugId: product.nationalDrugId,
      nationalRegistrationNumber: product.nationalRegistrationNumber,
      status: product.status,
      saleUnitName: product.saleUnitName ?? product.units.find((u) => u.isBaseUnit)?.unitName ?? catalogT()('shared.defaultSaleUnit'),
      minStockQty: product.minStockQty,
    });
    const saleUnitId =
      product.units.find((u) => u.isSaleUnit)?.id ??
      product.units.find((u) => u.isBaseUnit)?.id ??
      product.units[0]?.id;
    setDraftProductUnitId(saleUnitId ?? '');
  };

  const buildGeneralPayload = () => ({
    productCode: readTextField(form, 'productCode'),
    productName: readTextField(form, 'productName')!,
    genericName: readTextField(form, 'genericName'),
    drugType: form.getFieldValue('drugType') as number,
    categoryId: form.getFieldValue('categoryId') as string | undefined,
    brandId: form.getFieldValue('brandId') as string | undefined,
    description: readTextField(form, 'description'),
    nationalDrugId: readTextField(form, 'nationalDrugId'),
    nationalRegistrationNumber: readTextField(form, 'nationalRegistrationNumber'),
    status: (form.getFieldValue('status') as number | undefined) ?? 1,
    saleUnitName: readTextField(form, 'saleUnitName') ?? catalogT()('shared.defaultSaleUnit'),
    minStockQty: form.getFieldValue('minStockQty') as number | undefined,
  });

  const captureGeneralBaseline = () => {
    baselineGeneralRef.current = JSON.stringify(buildGeneralPayload());
    setGeneralDirty(false);
  };

  const hasUnsavedGeneralChanges = () => {
    if (generalDirty) return true;
    if (!baselineGeneralRef.current) return false;
    return JSON.stringify(buildGeneralPayload()) !== baselineGeneralRef.current;
  };

  const hasUnsavedDraftInputs = () =>
    draftBarcode.trim() !== '' ||
    draftImageUrl.trim() !== '' ||
    draftPrice != null ||
    draftIngredientId !== '' ||
    draftStrengthValue != null;

  const hasUnsavedChanges = () => hasUnsavedGeneralChanges() || hasUnsavedDraftInputs();

  const persistCommercial = (payload: ProductCommercialPayload): Promise<ProductDetail | undefined> => {
    const product = currentProduct();
    if (!product) return Promise.resolve(undefined);

    commercialRef.current = payload;
    setCommercial(payload);

    const run = async (): Promise<ProductDetail | undefined> => {
      const activeProduct = currentProduct();
      if (!activeProduct) return undefined;

      setCommercialSaving(true);
      try {
        const updated = await syncProductCommercial(activeProduct.id, commercialRef.current);
        const nextCommercial = toCommercialPayload(updated);
        commercialRef.current = nextCommercial;
        setCommercial(nextCommercial);
        productRef.current = updated;
        setSessionProduct(updated);
        onUpdated(updated);
        return updated;
      } catch (error) {
        if (displayProduct) syncCommercialFromProduct(displayProduct);
        msg.error(apiErrorMessage(error, t('productForm.messages.saveFailed')));
        throw error;
      } finally {
        setCommercialSaving(false);
      }
    };

    const chained = persistChainRef.current.then(run, run);
    persistChainRef.current = chained.catch(() => undefined);
    return chained;
  };

  const persistUnits = (payload: ProductUnitPayload[]): Promise<ProductDetail | undefined> => {
    const product = currentProduct();
    if (!product) return Promise.resolve(undefined);

    unitsRef.current = payload;
    setUnits(payload);

    const run = async (): Promise<ProductDetail | undefined> => {
      const activeProduct = currentProduct();
      if (!activeProduct) return undefined;

      setUnitsSaving(true);
      try {
        const updated = await syncProductUnits(activeProduct.id, unitsRef.current);
        syncUnitsFromProduct(updated);
        syncCommercialFromProduct(updated);
        productRef.current = updated;
        setSessionProduct(updated);
        onUpdated(updated);
        return updated;
      } catch (error) {
        if (displayProduct) syncUnitsFromProduct(displayProduct);
        msg.error(apiErrorMessage(error, t('productForm.messages.saveUnitsFailed')));
        throw error;
      } finally {
        setUnitsSaving(false);
      }
    };

    const chained = unitsPersistChainRef.current.then(run, run);
    unitsPersistChainRef.current = chained.catch(() => undefined);
    return chained;
  };

  const persistIngredients = (payload: IngredientRow[]): Promise<ProductDetail | undefined> => {
    const product = currentProduct();
    if (!product) return Promise.resolve(undefined);

    ingredientsRef.current = payload;
    setIngredientRows(payload);

    const run = async (): Promise<ProductDetail | undefined> => {
      const activeProduct = currentProduct();
      if (!activeProduct) return undefined;

      setIngredientsSaving(true);
      try {
        const body: ProductIngredientPayload[] = payload.map((row) => ({
          ingredientId: row.ingredientId,
          strengthValue: row.strengthValue,
          strengthUnit: row.strengthUnit,
        }));
        const updated = await syncProductIngredients(activeProduct.id, body);
        syncIngredientsFromProduct(updated);
        productRef.current = updated;
        setSessionProduct(updated);
        onUpdated(updated);
        return updated;
      } catch (error) {
        if (displayProduct) syncIngredientsFromProduct(displayProduct);
        msg.error(apiErrorMessage(error, t('productForm.messages.saveIngredientsFailed')));
        throw error;
      } finally {
        setIngredientsSaving(false);
      }
    };

    const chained = ingredientsPersistChainRef.current.then(run, run);
    ingredientsPersistChainRef.current = chained.catch(() => undefined);
    return chained;
  };

  const ensureGeneralFilled = async (tab: Exclude<TabKey, 'general'>): Promise<boolean> => {
    try {
      await form.validateFields(['productName', 'drugType']);
      return true;
    } catch {
      msg.warning(
        t('productForm.messages.fillGeneralBefore', {
          section:
            tab === 'details'
              ? t('productForm.sections.detailsTab')
              : t('productForm.sections.ingredientsTab'),
        }),
      );
      return false;
    }
  };

  const validateGeneralForCreate = async (): Promise<boolean> => {
    try {
      await form.validateFields(['productName', 'drugType']);
    } catch {
      msg.warning(t('productForm.messages.requireNameAndDrugType'));
      return false;
    }

    const productName = readTextField(form, 'productName') ?? '';
    let matches = similarMatches;
    if (!matches.length && productName.trim()) {
      const result = await checkSimilarProductNames(productName);
      matches = result.matches;
      setSimilarMatches(matches);
    }
    if (matches.length > 0) {
      return confirmSaveDespiteSimilarName(matches);
    }
    return true;
  };

  const createProductFirstTime = async (): Promise<ProductDetail | null> => {
    if (currentProduct()) return currentProduct();
    const allowed = await validateGeneralForCreate();
    if (!allowed) return null;

    setSaving(true);
    try {
      const created = await createProduct(buildGeneralPayload());
      productRef.current = created;
      setSessionProduct(created);
      syncCommercialFromProduct(created);
      const saleUnitId =
        created.units.find((u) => u.isSaleUnit)?.id ??
        created.units.find((u) => u.isBaseUnit)?.id ??
        created.units[0]?.id;
      if (saleUnitId) setDraftProductUnitId(saleUnitId);
      onCreated(created);
      setShowCreatedHint(true);
      captureGeneralBaseline();
      msg.success(t('productForm.messages.created', { code: created.productCode }));
      return created;
    } catch (error) {
      if (isAxiosError(error)) {
        msg.error(apiErrorMessage(error, t('productForm.messages.createFailed')));
      }
      return null;
    } finally {
      setSaving(false);
    }
  };

  const handleTabChange = async (key: string) => {
    const tab = key as TabKey;
    if (tab === 'general') {
      setActiveTab(tab);
      return;
    }

    if (!(await ensureGeneralFilled(tab))) return;

    if (!hasPersistedId) {
      const created = await createProductFirstTime();
      if (!created) return;
    }

    setActiveTab(tab);
  };

  const loadNextProductCode = useCallback(async () => {
    setLoadingCode(true);
    try {
      const code = await fetchNextProductCode();
      form.setFieldsValue({ productCode: code, drugType: 1, status: 1, saleUnitName: catalogT()('shared.defaultSaleUnit') });
    } catch (error) {
      msg.error(apiErrorMessage(error, t('productForm.messages.fetchCodeFailed')));
    } finally {
      setLoadingCode(false);
    }
  }, [form, msg, t]);

  useEffect(() => {
    if (!open) return;
    void Promise.all([fetchCategoryLookups(), fetchBrandLookups(), fetchIngredientLookups()])
      .then(([cats, brs, ings]) => {
        setCategories(cats);
        setBrands(brs);
        setIngredientOptions(ings);
      })
      .catch(() => {});
  }, [open]);

  useEffect(() => {
    if (!open || editing !== null) return;

    let cancelled = false;
    form.resetFields();
    resetDrafts();
    setLoadingCode(true);

    void fetchNextProductCode()
      .then((code) => {
        if (cancelled) return;
        form.setFieldsValue({
          productCode: code,
          drugType: nationalPrefill?.drugType ?? 1,
          status: 1,
          saleUnitName: nationalPrefill?.saleUnitName ?? catalogT()('shared.defaultSaleUnit'),
          productName: nationalPrefill?.productName,
          genericName: nationalPrefill?.genericName,
          description: nationalPrefill?.description,
          nationalDrugId: nationalPrefill?.nationalDrugId,
          nationalRegistrationNumber: nationalPrefill?.nationalRegistrationNumber,
        });
        captureGeneralBaseline();
      })
      .catch((error) => {
        if (!cancelled) msg.error(apiErrorMessage(error, t('productForm.messages.fetchCodeFailed')));
      })
      .finally(() => {
        if (!cancelled) setLoadingCode(false);
      });

    return () => {
      cancelled = true;
    };
  }, [open, editing, nationalPrefill, form, msg, t]);

  useEffect(() => {
    if (!open || !editing) return;
    setSessionProduct(editing);
    loadEditingProduct(editing);
    captureGeneralBaseline();
  }, [open, editing, form]);

  const runNameSimilarityCheck = async (name: string) => {
    const trimmed = name.trim();
    if (!trimmed) {
      setSimilarMatches([]);
      return;
    }
    try {
      const result = await checkSimilarProductNames(trimmed, displayProduct?.id ?? editing?.id);
      setSimilarMatches(result.matches);
    } catch {
      setSimilarMatches([]);
    }
  };

  const confirmSaveDespiteSimilarName = (matches: SimilarProductMatch[]) =>
    new Promise<boolean>((resolve) => {
      modal.confirm({
        title: t('productForm.similarName.confirmTitle'),
        width: 520,
        zIndex: 1200,
        content: (
          <div>
            <Typography.Paragraph style={{ marginBottom: 8 }}>
              {t('productForm.similarName.confirmBody')}
            </Typography.Paragraph>
            <ul style={{ margin: 0, paddingLeft: 20 }}>
              {matches.map((m) => (
                <li key={m.id}>
                  <Typography.Text strong>{m.productCode}</Typography.Text> — {m.productName}{' '}
                  <Typography.Text type="secondary">({Math.round(m.similarityScore * 100)}%)</Typography.Text>
                </li>
              ))}
            </ul>
          </div>
        ),
        okText: t('productForm.actions.stillSave'),
        cancelText: tc('actions.cancel'),
        onOk: () => resolve(true),
        onCancel: () => resolve(false),
      });
    });

  const handleAddBarcode = async () => {
    if (!currentProduct()) {
      msg.warning(t('productForm.messages.fillGeneralBeforeBarcode'));
      return;
    }
    const barcode = draftBarcode.trim();
    if (!barcode) {
      msg.warning(t('productForm.messages.enterBarcode'));
      return;
    }
    if (commercialRef.current.barcodes.some((b) => b.barcode === barcode)) {
      msg.warning(t('productForm.messages.barcodeDuplicate'));
      return;
    }

    let barcodeCheck: BarcodeCheckResult = { isAvailable: true };
    try {
      barcodeCheck = await checkBarcode(barcode, currentProduct()?.id);
    } catch {
      /* ignore check failure */
    }
    if (!barcodeCheck.isAvailable) {
      msg.error(
        t('productForm.messages.barcodeInUse', {
          code: barcodeCheck.existingProductCode,
          name: barcodeCheck.existingProductName,
        }),
      );
      return;
    }

    const base = commercialRef.current;
    const nextBarcodes = [
      ...base.barcodes,
      { barcode, isPrimary: base.barcodes.length === 0, barcodeType: draftBarcodeType },
    ];

    try {
      await persistCommercial({ ...base, barcodes: nextBarcodes });
      setDraftBarcode('');
      msg.success(t('productForm.messages.barcodeAdded'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleSetPrimaryBarcode = async (barcode: string) => {
    const base = commercialRef.current;
    const nextBarcodes = base.barcodes.map((b) => ({
      ...b,
      isPrimary: b.barcode === barcode,
    }));
    try {
      await persistCommercial({ ...base, barcodes: nextBarcodes });
      msg.success(t('productForm.messages.primaryBarcodeSet'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleRemoveBarcode = async (barcode: string) => {
    const base = commercialRef.current;
    const remaining = base.barcodes.filter((b) => b.barcode !== barcode);
    const primaryBarcode = remaining.find((b) => b.isPrimary)?.barcode ?? remaining[0]?.barcode;
    const nextBarcodes = remaining.map((b) => ({
      ...b,
      isPrimary: b.barcode === primaryBarcode,
    }));
    try {
      await persistCommercial({ ...base, barcodes: nextBarcodes });
      msg.success(t('productForm.messages.barcodeRemoved'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleAddPrice = async () => {
    if (!currentProduct()) {
      msg.warning(t('productForm.messages.fillGeneralBeforePrice'));
      return;
    }
    const unitId = draftProductUnitId || defaultUnitId;
    if (!unitId) {
      msg.warning(t('productForm.messages.noUnits'));
      return;
    }
    if (!draftPrice || draftPrice <= 0) {
      msg.warning(t('productForm.messages.invalidPrice'));
      return;
    }
    if (commercialRef.current.prices.some((p) => p.priceType === draftPriceType && p.productUnitId === unitId)) {
      msg.warning(t('productForm.messages.priceDuplicate'));
      return;
    }

    const base = commercialRef.current;
    const nextPrices = [
      ...base.prices,
      { priceType: draftPriceType, price: draftPrice, productUnitId: unitId },
    ];

    try {
      await persistCommercial({ ...base, prices: nextPrices });
      setDraftPrice(undefined);
      msg.success(t('productForm.messages.priceAdded'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleRemovePrice = async (priceType: number, productUnitId: string) => {
    const base = commercialRef.current;
    const nextPrices = base.prices.filter(
      (p) => !(p.priceType === priceType && p.productUnitId === productUnitId),
    );
    try {
      await persistCommercial({ ...base, prices: nextPrices });
      msg.success(t('productForm.messages.priceRemoved'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleAddImage = async (url: string) => {
    if (!currentProduct()) {
      msg.warning(t('productForm.messages.fillGeneralBeforeImage'));
      return;
    }
    const trimmed = url.trim();
    if (!trimmed) return;
    if (commercialRef.current.images.some((img) => img.imageUrl === trimmed)) {
      msg.warning(t('productForm.messages.imageDuplicate'));
      return;
    }

    const base = commercialRef.current;
    const nextImages = [
      ...base.images,
      { imageUrl: trimmed, isPrimary: base.images.length === 0, sortOrder: base.images.length },
    ];

    try {
      await persistCommercial({ ...base, images: nextImages });
      msg.success(t('productForm.messages.imageAdded'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleAddImageFromUrl = async () => {
    const url = draftImageUrl.trim();
    if (!url) {
      msg.warning(t('productForm.messages.enterImageUrl'));
      return;
    }
    await handleAddImage(url);
    setDraftImageUrl('');
  };

  const handleImageUpload = async (options: UploadRequestOption) => {
    const file = options.file as File;
    setUploading(true);
    try {
      const url = await uploadImage(file);
      await handleAddImage(url);
      options.onSuccess?.(url);
    } catch (error) {
      const text = apiErrorMessage(error, t('productForm.messages.uploadFailed'));
      msg.error(text);
      options.onError?.(new Error(text));
    } finally {
      setUploading(false);
    }
  };

  const handleSetPrimaryImage = async (imageUrl: string) => {
    const base = commercialRef.current;
    const nextImages = base.images.map((img, index) => ({
      ...img,
      isPrimary: img.imageUrl === imageUrl,
      sortOrder: index,
    }));
    try {
      await persistCommercial({ ...base, images: nextImages });
      msg.success(t('productForm.messages.primaryImageSet'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleRemoveImage = async (imageUrl: string) => {
    const base = commercialRef.current;
    const remaining = base.images.filter((img) => img.imageUrl !== imageUrl);
    const primaryUrl = remaining.find((img) => img.isPrimary)?.imageUrl ?? remaining[0]?.imageUrl;
    const nextImages = remaining.map((img, index) => ({
      imageUrl: img.imageUrl,
      isPrimary: img.imageUrl === primaryUrl,
      sortOrder: index,
    }));
    try {
      await persistCommercial({ ...base, images: nextImages });
      msg.success(t('productForm.messages.imageRemoved'));
    } catch {
      /* persistCommercial đã báo lỗi */
    }
  };

  const handleAddUnit = async () => {
    if (!currentProduct()) {
      msg.warning(t('productForm.messages.fillGeneralBeforeUnits'));
      return;
    }
    const unitName = draftUnitName.trim();
    if (!unitName) {
      msg.warning(t('productForm.messages.enterUnitName'));
      return;
    }
    const base = unitsRef.current;
    if (base.some((u) => u.unitName.toLowerCase() === unitName.toLowerCase())) {
      msg.warning(t('productForm.messages.unitDuplicate'));
      return;
    }
    const factor = draftConversionFactor ?? 1;
    if (factor <= 0) {
      msg.warning(t('productForm.messages.invalidConversionFactor'));
      return;
    }

    const nextUnits = [
      ...base,
      {
        unitName,
        conversionFactor: factor,
        isBaseUnit: false,
        isSaleUnit: draftUnitIsSale,
      },
    ];

    try {
      await persistUnits(nextUnits);
      setDraftUnitName(catalogT()('shared.defaultPackUnit'));
      setDraftConversionFactor(10);
      msg.success(t('productForm.messages.unitAdded'));
    } catch {
      /* persistUnits đã báo lỗi */
    }
  };

  const handleRemoveUnit = async (unitName: string) => {
    const base = unitsRef.current;
    const target = base.find((u) => u.unitName === unitName);
    if (!target) return;
    if (target.isBaseUnit) {
      msg.warning(t('productForm.messages.cannotRemoveBaseUnit'));
      return;
    }
    try {
      await persistUnits(base.filter((u) => u.unitName !== unitName));
      msg.success(t('productForm.messages.unitRemoved'));
    } catch {
      /* persistUnits đã báo lỗi */
    }
  };

  const handleToggleUnitSale = async (unitName: string) => {
    const base = unitsRef.current;
    const nextUnits = base.map((u) =>
      u.unitName === unitName ? { ...u, isSaleUnit: !u.isSaleUnit } : u,
    );
    if (!nextUnits.some((u) => u.isSaleUnit)) {
      msg.warning(t('productForm.messages.requireSaleUnit'));
      return;
    }
    try {
      await persistUnits(nextUnits);
      msg.success(t('productForm.messages.saleUnitUpdated'));
    } catch {
      /* persistUnits đã báo lỗi */
    }
  };

  const handleAddIngredient = async () => {
    if (!currentProduct()) {
      msg.warning(t('productForm.messages.fillGeneralBeforeIngredients'));
      return;
    }
    if (!draftIngredientId) {
      msg.warning(t('productForm.messages.selectIngredient'));
      return;
    }
    const option = ingredientOptions.find((o) => o.id === draftIngredientId);
    const base = ingredientsRef.current;
    if (base.some((row) => row.ingredientId === draftIngredientId)) {
      msg.warning(t('productForm.messages.ingredientDuplicate'));
      return;
    }

    const nextRows: IngredientRow[] = [
      ...base,
      {
        ingredientId: draftIngredientId,
        ingredientName: option?.name ?? '',
        ingredientCode: option?.code ?? '',
        strengthValue: draftStrengthValue,
        strengthUnit: draftStrengthUnit.trim() || undefined,
      },
    ];

    try {
      await persistIngredients(nextRows);
      setDraftIngredientId('');
      setDraftStrengthValue(undefined);
      applyGenericSuggestion(ingredientsRef.current, true);
      msg.success(t('productForm.messages.ingredientAdded'));
    } catch {
      /* persistIngredients đã báo lỗi */
    }
  };

  const handleRemoveIngredient = async (ingredientId: string) => {
    const base = ingredientsRef.current;
    try {
      await persistIngredients(base.filter((row) => row.ingredientId !== ingredientId));
      msg.success(t('productForm.messages.ingredientRemoved'));
    } catch {
      /* persistIngredients đã báo lỗi */
    }
  };

  const applyGenericSuggestion = useCallback(
    (rows: IngredientRow[], onlyIfEmpty = false) => {
      const suggestion = formatGenericFromIngredients(rows);
      if (!suggestion) {
        msg.info(t('productForm.messages.noIngredientsToSuggest'));
        return;
      }

      const current = readTextField(form, 'genericName') ?? '';
      if (!current.trim()) {
        form.setFieldsValue({ genericName: suggestion });
        setGeneralDirty(true);
        msg.success(t('productForm.messages.genericSuggested'));
        return;
      }

      if (onlyIfEmpty) return;

      modal.confirm({
        title: t('productForm.confirm.applyGenericTitle'),
        zIndex: 1200,
        content: (
          <div>
            <Typography.Paragraph style={{ marginBottom: 8 }}>
              {t('productForm.confirm.applyGenericBody')}
            </Typography.Paragraph>
            <Typography.Text strong>{suggestion}</Typography.Text>
          </div>
        ),
        okText: t('productForm.actions.apply'),
        cancelText: t('productForm.actions.keepCurrent'),
        onOk: () => {
          form.setFieldsValue({ genericName: suggestion });
          setGeneralDirty(true);
        },
      });
    },
    [form, msg, modal, t],
  );

  const saveGeneralChanges = async (): Promise<boolean> => {
    if (!hasPersistedId) {
      const created = await createProductFirstTime();
      return created !== null;
    }

    try {
      await form.validateFields(['productName', 'drugType']);

      const productName = readTextField(form, 'productName') ?? '';
      let matches = similarMatches;
      if (!matches.length && productName.trim()) {
        const result = await checkSimilarProductNames(productName, displayProduct?.id ?? editing?.id ?? undefined);
        matches = result.matches;
        setSimilarMatches(matches);
      }
      if (matches.length > 0) {
        const proceed = await confirmSaveDespiteSimilarName(matches);
        if (!proceed) return false;
      }

      setSaving(true);
      const updated = await updateProduct(displayProduct!.id, buildGeneralPayload());
      productRef.current = updated;
      setSessionProduct(updated);
      loadEditingProduct(updated);
      captureGeneralBaseline();
      onUpdated(updated);
      msg.success(t('productForm.messages.saved'));
      return true;
    } catch (error) {
      if (isAxiosError(error)) {
        msg.error(apiErrorMessage(error, t('productForm.messages.saveProductFailed')));
      }
      return false;
    } finally {
      setSaving(false);
    }
  };

  const confirmCloseIfDirty = () => {
    if (!hasUnsavedChanges()) {
      onClose();
      return;
    }
    modal.confirm({
      title: t('productForm.confirm.unsavedTitle'),
      content: t('productForm.confirm.unsavedBody'),
      okText: tc('actions.yes'),
      cancelText: tc('actions.no'),
      maskClosable: false,
      closable: false,
      zIndex: 1200,
      onOk: async () => {
        if (!(await saveGeneralChanges())) {
          return Promise.reject();
        }
        onClose();
      },
      onCancel: () => {
        onClose();
      },
    });
  };

  const tryCloseDrawer = () => {
    confirmCloseIfDirty();
  };

  const handleSkip = () => {
    confirmCloseIfDirty();
  };

  const handleSave = async () => {
    await saveGeneralChanges();
  };

  const generalFields = (
    <>
      {linkedNationalDrugId && (
        <Alert
          type="success"
          showIcon
          message={t('productForm.nationalDrug.linkedTitle')}
          description={
            <>
              {t('productForm.nationalDrug.nationalId')}: <strong>{linkedNationalDrugId}</strong>
              {linkedNationalReg && (
                <>
                  {' '}
                  · {t('productForm.nationalDrug.registrationNumber')}: <strong>{linkedNationalReg}</strong>
                </>
              )}
              {nationalPrefill?.suggestedBarcode && !hasPersistedId && (
                <>
                  <br />
                  {t('productForm.nationalDrug.suggestedBarcode')}:{' '}
                  <Typography.Text code>{nationalPrefill.suggestedBarcode}</Typography.Text>{' '}
                  {t('productForm.nationalDrug.suggestedBarcodeHint')}
                </>
              )}
            </>
          }
          style={{ marginBottom: 16 }}
        />
      )}
      <Form.Item name="nationalDrugId" hidden>
        <Input />
      </Form.Item>
      <Form.Item name="nationalRegistrationNumber" hidden>
        <Input />
      </Form.Item>
      <Form.Item label={t('productForm.fields.productCode')}>
        <Space.Compact style={{ width: '100%' }}>
          <Form.Item name="productCode" noStyle>
            <Input
              disabled
              placeholder={loadingCode ? t('productForm.placeholders.loadingCode') : undefined}
              style={{ width: 'calc(100% - 40px)' }}
            />
          </Form.Item>
          {!hasPersistedId && (
            <Button
              onClick={() => void loadNextProductCode()}
              loading={loadingCode}
              title={t('productForm.actions.refreshCode')}
            >
              ↻
            </Button>
          )}
        </Space.Compact>
      </Form.Item>
      <Form.Item name="productName" label={t('productForm.fields.productName')} rules={[{ required: true }]}>
        <Input onBlur={(e) => void runNameSimilarityCheck(e.target.value)} />
      </Form.Item>
      {similarMatches.length > 0 && (
        <Alert
          type="warning"
          showIcon
          message={
            similarMatches.some((m) => m.similarityScore >= 1)
              ? t('productForm.similarName.exact')
              : t('productForm.similarName.near')
          }
          description={
            <ul style={{ margin: '4px 0 0', paddingLeft: 20 }}>
              {similarMatches.map((m) => (
                <li key={m.id}>
                  {m.productCode} — {m.productName} ({Math.round(m.similarityScore * 100)}%)
                </li>
              ))}
            </ul>
          }
          style={{ marginBottom: 16 }}
        />
      )}
      <Form.Item
        label={t('productForm.fields.genericName')}
        extra={t('productForm.fields.genericNameExtra')}
      >
        <Space.Compact style={{ width: '100%' }}>
          <Form.Item name="genericName" noStyle>
            <Input
              placeholder={t('productForm.placeholders.genericName')}
              style={{ width: 'calc(100% - 88px)' }}
            />
          </Form.Item>
          <Button
            onClick={() => applyGenericSuggestion(ingredientRows)}
            disabled={!ingredientRows.length}
            title={t('productForm.actions.suggestGenericTitle')}
          >
            {t('productForm.actions.suggestGeneric')}
          </Button>
        </Space.Compact>
      </Form.Item>
      <Form.Item name="saleUnitName" label={t('productForm.fields.saleUnitName')} rules={[{ required: true }]}>
        <Select
          showSearch
          options={SALE_UNIT_OPTIONS}
          disabled={hasPersistedId}
          placeholder={t('productForm.placeholders.saleUnitName')}
        />
      </Form.Item>
      {!hasPersistedId && (
        <Typography.Text type="secondary" style={{ display: 'block', marginBottom: 16 }}>
          {t('productForm.fields.saleUnitHint')}
        </Typography.Text>
      )}
      <Form.Item name="drugType" label={t('productForm.fields.drugType')} rules={[{ required: true }]}>
        <Select options={drugTypeOptions} />
      </Form.Item>
      <Form.Item name="categoryId" label={t('productForm.fields.categoryId')}>
        <Select allowClear options={categories.map((c) => ({ value: c.id, label: c.name }))} />
      </Form.Item>
      <Form.Item name="brandId" label={t('productForm.fields.brandId')}>
        <Select allowClear options={brands.map((b) => ({ value: b.id, label: b.name }))} />
      </Form.Item>
      <Form.Item name="description" label={t('productForm.fields.description')}>
        <Input.TextArea rows={2} />
      </Form.Item>
      <Form.Item name="status" label={t('productForm.fields.status')}>
        <Select options={productStatusOptions} />
      </Form.Item>
      <Form.Item name="minStockQty" label={t('productForm.fields.minStockQty')}>
        <InputNumber
          min={0}
          precision={3}
          style={{ width: '100%' }}
          placeholder={t('productForm.placeholders.minStockQty')}
        />
      </Form.Item>
    </>
  );

  const unitsFields = (
    <Spin spinning={unitsSaving}>
      <div style={{ margin: '0 0 12px' }}>
        {units.length ? (
          <Space direction="vertical" style={{ width: '100%' }}>
            {units.map((u) => (
              <div
                key={u.id ?? u.unitName}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '6px 0',
                  borderBottom: '1px solid #f0f0f0',
                }}
              >
                <span>
                  <Typography.Text strong>{u.unitName}</Typography.Text>
                  {u.isBaseUnit && (
                    <Tag color="blue" style={{ marginLeft: 8 }}>
                      {t('productForm.unitTags.base')}
                    </Tag>
                  )}
                  {u.isSaleUnit && (
                    <Tag color="green" style={{ marginLeft: 4 }}>
                      {t('productForm.unitTags.sale')}
                    </Tag>
                  )}
                  {!u.isBaseUnit && (
                    <Typography.Text type="secondary">
                      {' '}
                      · {t('productForm.unitTags.conversion', { unit: u.unitName, factor: u.conversionFactor })}
                    </Typography.Text>
                  )}
                </span>
                <Space size={4}>
                  <Button
                    type="text"
                    size="small"
                    icon={u.isSaleUnit ? <StarFilled style={{ color: '#52c41a' }} /> : <StarOutlined />}
                    onClick={() => void handleToggleUnitSale(u.unitName)}
                    title={t('productForm.unitTags.saleUnitTitle')}
                    disabled={u.isBaseUnit}
                  />
                  {!u.isBaseUnit && (
                    <Button
                      type="text"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                      onClick={() => void handleRemoveUnit(u.unitName)}
                    />
                  )}
                </Space>
              </div>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">{t('productForm.empty.units')}</Typography.Text>
        )}
      </div>
      <Space wrap style={{ width: '100%' }}>
        <Select
          showSearch
          style={{ width: 120 }}
          value={draftUnitName}
          onChange={setDraftUnitName}
          options={SALE_UNIT_OPTIONS.filter((o) => !units.some((u) => u.unitName === o.value))}
          placeholder={t('productForm.placeholders.unit')}
        />
        <InputNumber
          min={1}
          placeholder={t('productForm.fields.conversionFactor')}
          value={draftConversionFactor}
          onChange={(v) => setDraftConversionFactor(v ?? undefined)}
          style={{ width: 100 }}
        />
        <Select
          style={{ width: 110 }}
          value={draftUnitIsSale ? 1 : 0}
          onChange={(v) => setDraftUnitIsSale(v === 1)}
          options={[
            { value: 1, label: t('productForm.unitTags.saleAllowed') },
            { value: 0, label: t('productForm.unitTags.saleNotAllowed') },
          ]}
        />
        <Button type="primary" onClick={() => void handleAddUnit()} loading={unitsSaving}>
          {tc('actions.add')}
        </Button>
      </Space>
    </Spin>
  );

  const barcodeFields = (
    <Spin spinning={commercialSaving}>
      <div style={{ margin: '0 0 12px', minHeight: 32 }}>
        {commercial.barcodes.length ? (
          commercial.barcodes.map((b) => (
            <Tag key={b.barcode} style={{ marginBottom: 4, padding: '4px 8px' }}>
              <Space size={4}>
                <Button
                  type="text"
                  size="small"
                  icon={b.isPrimary ? <StarFilled style={{ color: '#faad14' }} /> : <StarOutlined />}
                  onClick={() => void handleSetPrimaryBarcode(b.barcode)}
                  title={t('productForm.actions.setPrimaryBarcode')}
                />
                <span>{b.barcode}</span>
                <Button
                  type="text"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => void handleRemoveBarcode(b.barcode)}
                />
              </Space>
            </Tag>
          ))
        ) : (
          <Typography.Text type="secondary">{t('productForm.empty.barcodes')}</Typography.Text>
        )}
      </div>
      <Space.Compact style={{ width: '100%' }}>
        <Select
          style={{ width: 130 }}
          value={draftBarcodeType}
          onChange={setDraftBarcodeType}
          options={barcodeTypeOptions}
        />
        <Input
          placeholder={t('productForm.placeholders.barcode')}
          value={draftBarcode}
          onChange={(e) => setDraftBarcode(e.target.value)}
          onPressEnter={() => void handleAddBarcode()}
          style={{ width: 'calc(100% - 202px)' }}
        />
        <Button type="primary" onClick={() => void handleAddBarcode()} loading={commercialSaving}>
          {tc('actions.add')}
        </Button>
      </Space.Compact>
    </Spin>
  );

  const priceFields = (
    <Spin spinning={commercialSaving}>
      <div style={{ margin: '0 0 12px' }}>
        {commercial.prices.length ? (
          <Space direction="vertical" style={{ width: '100%' }}>
            {commercial.prices.map((p) => {
              const unitName =
                unitOptions.find((u) => u.value === p.productUnitId)?.label ??
                displayProduct?.units.find((u) => u.id === p.productUnitId)?.unitName ??
                '—';
              return (
              <div
                key={`${p.priceType}-${p.productUnitId}`}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '6px 0',
                  borderBottom: '1px solid #f0f0f0',
                }}
              >
                <span>
                  <Typography.Text strong>{priceTypeLabel(p.priceType)}</Typography.Text>
                  {' · '}
                  {unitName}
                  {' · '}
                  {formatDisplayMoney(p.price)}
                </span>
                <Button
                  type="text"
                  danger
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={() => void handleRemovePrice(p.priceType, p.productUnitId ?? defaultUnitId)}
                />
              </div>
              );
            })}
          </Space>
        ) : (
          <Typography.Text type="secondary">{t('productForm.empty.prices')}</Typography.Text>
        )}
      </div>
      <Space wrap style={{ width: '100%' }}>
        <Select
          style={{ width: 130 }}
          value={draftPriceType}
          onChange={setDraftPriceType}
          options={priceTypeOptions}
        />
        <Select
          style={{ width: 100 }}
          value={draftProductUnitId || defaultUnitId || undefined}
          onChange={setDraftProductUnitId}
          options={unitOptions}
          placeholder={t('productForm.placeholders.unit')}
          disabled={!unitOptions.length}
        />
        <InputNumber
          placeholder={t('productForm.placeholders.salePrice')}
          value={draftPrice}
          onChange={(v) => setDraftPrice(v && v > 0 ? v : undefined)}
          style={{ ...moneyInputNumberStyle, width: 160 }}
          {...moneyInputNumberPropsAllowZeroSuffix}
        />
        <Button type="primary" onClick={() => void handleAddPrice()} loading={commercialSaving}>
          {tc('actions.add')}
        </Button>
      </Space>
    </Spin>
  );

  const imageFields = (
    <Spin spinning={commercialSaving}>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: 8, margin: '0 0 12px', minHeight: 80 }}>
        {commercial.images.length ? (
          commercial.images.map((img) => (
            <div key={img.imageUrl} style={{ textAlign: 'center' }}>
              <Avatar shape="square" size={72} src={withUploadAuth(img.imageUrl)} alt="" />
              <div style={{ marginTop: 4 }}>
                <Button
                  type="text"
                  size="small"
                  icon={img.isPrimary ? <StarFilled style={{ color: '#faad14' }} /> : <StarOutlined />}
                  onClick={() => void handleSetPrimaryImage(img.imageUrl)}
                  title={t('productForm.actions.setPrimaryImage')}
                />
                <Button
                  type="text"
                  size="small"
                  danger
                  icon={<DeleteOutlined />}
                  onClick={() => void handleRemoveImage(img.imageUrl)}
                />
              </div>
            </div>
          ))
        ) : (
          <Typography.Text type="secondary">{t('productForm.empty.images')}</Typography.Text>
        )}
      </div>
      <Space direction="vertical" style={{ width: '100%' }}>
        <Upload
          accept="image/jpeg,image/png,image/webp"
          showUploadList={false}
          customRequest={handleImageUpload}
          disabled={uploading || commercialSaving}
        >
          <Button icon={<UploadOutlined />} loading={uploading}>
            {t('productForm.actions.uploadImage')}
          </Button>
        </Upload>
        <Space.Compact style={{ width: '100%' }}>
          <Input
            placeholder={t('productForm.placeholders.imageUrl')}
            value={draftImageUrl}
            onChange={(e) => setDraftImageUrl(e.target.value)}
            onPressEnter={() => void handleAddImageFromUrl()}
            style={{ width: 'calc(100% - 72px)' }}
          />
          <Button type="primary" onClick={() => void handleAddImageFromUrl()} loading={commercialSaving}>
            {tc('actions.add')}
          </Button>
        </Space.Compact>
      </Space>
    </Spin>
  );

  const productDetailsFields = (
    <>
      <Typography.Paragraph type="secondary" style={{ marginBottom: 16 }}>
        {t('productForm.sections.detailsHint')}
      </Typography.Paragraph>

      <section style={{ marginBottom: 20 }}>
        <Typography.Text strong>
          {t('productForm.sections.units')}
          {units.length ? ` (${units.length})` : ''}
        </Typography.Text>
        <div style={{ marginTop: 8 }}>{unitsFields}</div>
      </section>

      <Divider style={{ margin: '16px 0' }} />

      <section style={{ marginBottom: 20 }}>
        <Typography.Text strong>
          {t('productForm.sections.barcode')}
          {commercial.barcodes.length ? ` (${commercial.barcodes.length})` : ''}
        </Typography.Text>
        <div style={{ marginTop: 8 }}>{barcodeFields}</div>
      </section>

      <Divider style={{ margin: '16px 0' }} />

      <section style={{ marginBottom: 20 }}>
        <Typography.Text strong>
          {t('productForm.sections.prices')}
          {commercial.prices.length ? ` (${commercial.prices.length})` : ''}
        </Typography.Text>
        <div style={{ marginTop: 8 }}>{priceFields}</div>
      </section>

      <Divider style={{ margin: '16px 0' }} />

      <section>
        <Typography.Text strong>
          {t('productForm.sections.images')}
          {commercial.images.length ? ` (${commercial.images.length})` : ''}
        </Typography.Text>
        <div style={{ marginTop: 8 }}>{imageFields}</div>
      </section>
    </>
  );

  const ingredientsFields = (
    <Spin spinning={ingredientsSaving}>
      <Typography.Text type="secondary">{t('productForm.sections.ingredients')}</Typography.Text>
      <div style={{ margin: '8px 0 12px' }}>
        {ingredientRows.length ? (
          <Space direction="vertical" style={{ width: '100%' }}>
            {ingredientRows.map((row) => (
              <div
                key={row.ingredientId}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'space-between',
                  padding: '6px 0',
                  borderBottom: '1px solid #f0f0f0',
                }}
              >
                <span>
                  <Typography.Text strong>{row.ingredientName || row.ingredientCode}</Typography.Text>
                  {(row.strengthValue != null || row.strengthUnit) && (
                    <Typography.Text type="secondary">
                      {' '}
                      · {row.strengthValue ?? '—'} {row.strengthUnit ?? ''}
                    </Typography.Text>
                  )}
                </span>
                <Button
                  type="text"
                  danger
                  size="small"
                  icon={<DeleteOutlined />}
                  onClick={() => void handleRemoveIngredient(row.ingredientId)}
                />
              </div>
            ))}
          </Space>
        ) : (
          <Typography.Text type="secondary">{t('productForm.empty.ingredients')}</Typography.Text>
        )}
      </div>
      <Space wrap style={{ width: '100%' }}>
        <Select
          showSearch
          optionFilterProp="label"
          style={{ minWidth: 200 }}
          value={draftIngredientId || undefined}
          onChange={setDraftIngredientId}
          options={ingredientOptions
            .filter((o) => !ingredientRows.some((r) => r.ingredientId === o.id))
            .map((o) => ({ value: o.id, label: `${o.code} — ${o.name}` }))}
          placeholder={t('productForm.placeholders.ingredient')}
        />
        <InputNumber
          min={0}
          placeholder={t('productForm.fields.strengthValue')}
          value={draftStrengthValue}
          onChange={(v) => setDraftStrengthValue(v ?? undefined)}
          style={{ width: 110 }}
        />
        <Input
          placeholder={t('productForm.fields.strengthUnit')}
          value={draftStrengthUnit}
          onChange={(e) => setDraftStrengthUnit(e.target.value)}
          style={{ width: 72 }}
        />
        <Button type="primary" onClick={() => void handleAddIngredient()} loading={ingredientsSaving}>
          {tc('actions.add')}
        </Button>
      </Space>
    </Spin>
  );

  return (
    <Drawer
      title={
        hasPersistedId
          ? t('productForm.title.edit', { code: displayProduct?.productCode ?? '' })
          : t('productForm.title.create')
      }
      width={580}
      open={open}
      onClose={tryCloseDrawer}
      forceRender
      extra={
        <Space>
          <Button onClick={handleSkip}>{t('productForm.actions.skip')}</Button>
          {hasPersistedId && (
            <Popconfirm
              title={t('products.deleteConfirm')}
              onConfirm={async () => {
                try {
                  await deleteProduct(displayProduct!.id);
                  msg.success(t('productForm.messages.deleted'));
                  onClose();
                  onUpdated();
                } catch (error) {
                  msg.error(apiErrorMessage(error, t('productForm.messages.deleteFailed')));
                }
              }}
            >
              <Button danger>{tc('actions.delete')}</Button>
            </Popconfirm>
          )}
          <Button type="primary" loading={saving} onClick={() => void handleSave()}>
            {hasPersistedId ? t('productForm.actions.saveProduct') : t('productForm.actions.createProduct')}
          </Button>
        </Space>
      }
    >
      <Form form={form} layout="vertical" onValuesChange={() => setGeneralDirty(true)}>
        {showCreatedHint && hasPersistedId && (
          <Alert
            type="success"
            showIcon
            closable
            onClose={() => setShowCreatedHint(false)}
            message={t('productForm.messages.createdHint', { code: displayProduct?.productCode ?? '' })}
            style={{ marginBottom: 16 }}
          />
        )}
        <Tabs
          activeKey={activeTab}
          onChange={(key) => void handleTabChange(key)}
          items={[
            { key: 'general', label: t('productForm.tabs.general'), children: generalFields },
            { key: 'details', label: t('productForm.tabs.details'), children: productDetailsFields },
            { key: 'ingredients', label: t('productForm.tabs.ingredients'), children: ingredientsFields },
          ]}
        />
      </Form>
    </Drawer>
  );
}
