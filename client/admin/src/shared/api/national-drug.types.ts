export type NationalDrugConnectionStatus = {
  mode: string;
  modeLabel: string;
  isLive: boolean;
  message?: string;
};

export type NationalDrugFieldMap = {
  nationalField: string;
  nationalLabel: string;
  productField: string;
  productLabel: string;
  notes?: string;
};

export type NationalDrugListItem = {
  drugId: string;
  registrationNumber: string;
  productName: string;
  activeIngredient?: string;
  strength?: string;
  dosageForm?: string;
  unitName?: string;
  manufacturer?: string;
  drugCategoryLabel: string;
};

export type NationalDrugDetail = NationalDrugListItem & {
  packaging?: string;
  countryOfOrigin?: string;
  drugCategoryCode: string;
  barcode?: string;
  atcCode?: string;
  routeOfAdministration?: string;
  registrationExpiryDate?: string;
};

export type NationalDrugProductPrefill = {
  drugId: string;
  registrationNumber: string;
  productName: string;
  genericName?: string;
  drugType: number;
  saleUnitName: string;
  description?: string;
  suggestedBarcode?: string;
};

export type PagedNationalDrugList = {
  items: NationalDrugListItem[];
  total: number;
  page: number;
  pageSize: number;
};

/** Giá trị điền sẵn form tạo SP từ bản ghi QG. */
export type ProductFormNationalPrefill = {
  productName?: string;
  genericName?: string;
  drugType?: number;
  saleUnitName?: string;
  description?: string;
  nationalDrugId?: string;
  nationalRegistrationNumber?: string;
  suggestedBarcode?: string;
};

export function mapNationalPrefillToProductForm(
  prefill: NationalDrugProductPrefill,
): ProductFormNationalPrefill {
  return {
    productName: prefill.productName,
    genericName: prefill.genericName,
    drugType: prefill.drugType,
    saleUnitName: prefill.saleUnitName,
    description: prefill.description,
    nationalDrugId: prefill.drugId,
    nationalRegistrationNumber: prefill.registrationNumber,
    suggestedBarcode: prefill.suggestedBarcode,
  };
}
