import { http } from '@/shared/api/http';

export type Qd540Table1Row = {
  maThuoc: string;
  tenThuoc: string;
  soDangKy: string;
  tenHoatChat: string;
  nongDoHamLuong: string;
  nhaSanXuat: string;
  nuocSanXuat: string;
  nhaNhapKhau: string;
  quyCachDongGoi: string;
  dangBaoChe: string;
  donViDongGoiNn: string;
  giaBanLe: number;
  soLo: string;
  hanDung: number;
  soLuongNhap: number;
  soLuongBan: number;
  soLuongTon: number;
  donViBThuocChoCsbl: string;
  soHoaDonMThuoc: string;
  ngayNhap?: number | null;
  ngayBan?: number | null;
  maCoSoBanLe: string;
  maCoSoBanBuon: string;
};

export type Qd540Table1ExportResult = {
  rows: Qd540Table1Row[];
  warnings: string[];
  skippedRows: number;
};

export type Qd540Table1Query = {
  from: string;
  to: string;
  branchId?: string;
};

function normalizeRow(row: Record<string, unknown>): Qd540Table1Row {
  const pick = (camel: string, pascal: string) => row[camel] ?? row[pascal];
  return {
    maThuoc: String(pick('maThuoc', 'MaThuoc') ?? ''),
    tenThuoc: String(pick('tenThuoc', 'TenThuoc') ?? ''),
    soDangKy: String(pick('soDangKy', 'SoDangKy') ?? ''),
    tenHoatChat: String(pick('tenHoatChat', 'TenHoatChat') ?? ''),
    nongDoHamLuong: String(pick('nongDoHamLuong', 'NongDoHamLuong') ?? ''),
    nhaSanXuat: String(pick('nhaSanXuat', 'NhaSanXuat') ?? ''),
    nuocSanXuat: String(pick('nuocSanXuat', 'NuocSanXuat') ?? ''),
    nhaNhapKhau: String(pick('nhaNhapKhau', 'NhaNhapKhau') ?? ''),
    quyCachDongGoi: String(pick('quyCachDongGoi', 'QuyCachDongGoi') ?? ''),
    dangBaoChe: String(pick('dangBaoChe', 'DangBaoChe') ?? ''),
    donViDongGoiNn: String(pick('donViDongGoiNn', 'DonViDongGoiNn') ?? ''),
    giaBanLe: Number(pick('giaBanLe', 'GiaBanLe') ?? 0),
    soLo: String(pick('soLo', 'SoLo') ?? ''),
    hanDung: Number(pick('hanDung', 'HanDung') ?? 0),
    soLuongNhap: Number(pick('soLuongNhap', 'SoLuongNhap') ?? 0),
    soLuongBan: Number(pick('soLuongBan', 'SoLuongBan') ?? 0),
    soLuongTon: Number(pick('soLuongTon', 'SoLuongTon') ?? 0),
    donViBThuocChoCsbl: String(pick('donViBThuocChoCsbl', 'DonViBThuocChoCsbl') ?? ''),
    soHoaDonMThuoc: String(pick('soHoaDonMThuoc', 'SoHoaDonMThuoc') ?? ''),
    ngayNhap: (pick('ngayNhap', 'NgayNhap') as number | null | undefined) ?? null,
    ngayBan: (pick('ngayBan', 'NgayBan') as number | null | undefined) ?? null,
    maCoSoBanLe: String(pick('maCoSoBanLe', 'MaCoSoBanLe') ?? ''),
    maCoSoBanBuon: String(pick('maCoSoBanBuon', 'MaCoSoBanBuon') ?? ''),
  };
}

export async function previewQd540Table1(query: Qd540Table1Query): Promise<Qd540Table1ExportResult> {
  const { data } = await http.get<Record<string, unknown>>('/pharmacy/integration/qd540/table1', {
    params: query,
  });
  const rawRows = (data.rows ?? data.Rows ?? []) as Record<string, unknown>[];
  const rawWarnings = (data.warnings ?? data.Warnings ?? []) as string[];
  return {
    rows: rawRows.map(normalizeRow),
    warnings: rawWarnings,
    skippedRows: Number(data.skippedRows ?? data.SkippedRows ?? 0),
  };
}

export async function exportQd540Table1(query: Qd540Table1Query): Promise<Blob> {
  const { data } = await http.get<Blob>('/pharmacy/integration/qd540/table1.csv', {
    params: query,
    responseType: 'blob',
  });
  return data;
}
