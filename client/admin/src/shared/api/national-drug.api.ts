import { http } from '@/shared/api/http';
import type {
  NationalDrugConnectionStatus,
  NationalDrugDetail,
  NationalDrugFieldMap,
  NationalDrugListItem,
  NationalDrugProductPrefill,
  PagedNationalDrugList,
} from '@/shared/api/national-drug.types';

function normalizeConnectionStatus(row: Record<string, unknown>): NationalDrugConnectionStatus {
  return {
    mode: String(row.mode ?? row.Mode ?? 'mock'),
    modeLabel: String(row.modeLabel ?? row.ModeLabel ?? 'Mock'),
    isLive: Boolean(row.isLive ?? row.IsLive),
    message: (row.message ?? row.Message) as string | undefined,
  };
}

function normalizeFieldMap(row: Record<string, unknown>): NationalDrugFieldMap {
  return {
    nationalField: String(row.nationalField ?? row.NationalField ?? ''),
    nationalLabel: String(row.nationalLabel ?? row.NationalLabel ?? ''),
    productField: String(row.productField ?? row.ProductField ?? ''),
    productLabel: String(row.productLabel ?? row.ProductLabel ?? ''),
    notes: (row.notes ?? row.Notes) as string | undefined,
  };
}

function normalizeListItem(row: Record<string, unknown>): NationalDrugListItem {
  return {
    drugId: String(row.drugId ?? row.DrugId ?? ''),
    registrationNumber: String(row.registrationNumber ?? row.RegistrationNumber ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    activeIngredient: (row.activeIngredient ?? row.ActiveIngredient) as string | undefined,
    strength: (row.strength ?? row.Strength) as string | undefined,
    dosageForm: (row.dosageForm ?? row.DosageForm) as string | undefined,
    unitName: (row.unitName ?? row.UnitName) as string | undefined,
    manufacturer: (row.manufacturer ?? row.Manufacturer) as string | undefined,
    drugCategoryLabel: String(row.drugCategoryLabel ?? row.DrugCategoryLabel ?? ''),
  };
}

function normalizeDetail(row: Record<string, unknown>): NationalDrugDetail {
  return {
    ...normalizeListItem(row),
    packaging: (row.packaging ?? row.Packaging) as string | undefined,
    countryOfOrigin: (row.countryOfOrigin ?? row.CountryOfOrigin) as string | undefined,
    drugCategoryCode: String(row.drugCategoryCode ?? row.DrugCategoryCode ?? ''),
    barcode: (row.barcode ?? row.Barcode) as string | undefined,
    atcCode: (row.atcCode ?? row.AtcCode) as string | undefined,
    routeOfAdministration: (row.routeOfAdministration ?? row.RouteOfAdministration) as string | undefined,
    registrationExpiryDate: (row.registrationExpiryDate ?? row.RegistrationExpiryDate) as string | undefined,
  };
}

function normalizePrefill(row: Record<string, unknown>): NationalDrugProductPrefill {
  return {
    drugId: String(row.drugId ?? row.DrugId ?? ''),
    registrationNumber: String(row.registrationNumber ?? row.RegistrationNumber ?? ''),
    productName: String(row.productName ?? row.ProductName ?? ''),
    genericName: (row.genericName ?? row.GenericName) as string | undefined,
    drugType: Number(row.drugType ?? row.DrugType ?? 1),
    saleUnitName: String(row.saleUnitName ?? row.SaleUnitName ?? 'Viên'),
    description: (row.description ?? row.Description) as string | undefined,
    suggestedBarcode: (row.suggestedBarcode ?? row.SuggestedBarcode) as string | undefined,
  };
}

export async function fetchNationalDrugConnectionStatus(): Promise<NationalDrugConnectionStatus> {
  const { data } = await http.get<Record<string, unknown>>('/catalog/national-drugs/connection-status');
  return normalizeConnectionStatus(data);
}

export async function fetchNationalDrugFieldMap(): Promise<NationalDrugFieldMap[]> {
  const { data } = await http.get<Array<Record<string, unknown>>>('/catalog/national-drugs/field-map');
  return (data ?? []).map(normalizeFieldMap);
}

export async function searchNationalDrugs(params: {
  search?: string;
  page?: number;
  pageSize?: number;
}): Promise<PagedNationalDrugList> {
  const { data } = await http.get<Record<string, unknown>>('/catalog/national-drugs', { params });
  const items = ((data.items ?? data.Items ?? []) as Array<Record<string, unknown>>).map(normalizeListItem);
  return {
    items,
    total: Number(data.total ?? data.Total ?? items.length),
    page: Number(data.page ?? data.Page ?? 1),
    pageSize: Number(data.pageSize ?? data.PageSize ?? 20),
  };
}

export async function fetchNationalDrugDetail(drugId: string): Promise<NationalDrugDetail> {
  const { data } = await http.get<Record<string, unknown>>(`/catalog/national-drugs/${encodeURIComponent(drugId)}`);
  return normalizeDetail(data);
}

export async function fetchNationalDrugPrefill(drugId: string): Promise<NationalDrugProductPrefill> {
  const { data } = await http.get<Record<string, unknown>>(
    `/catalog/national-drugs/${encodeURIComponent(drugId)}/prefill`,
  );
  return normalizePrefill(data);
}
