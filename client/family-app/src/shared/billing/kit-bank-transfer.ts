/** KIT company bank transfer — SoT for manual / static VietQR checkout. */

export const KIT_BANK_TRANSFER = {
  accountNumber: '3900748543',
  /** NAPAS / VietQR BIN for BIDV */
  bankBin: '970418',
  bankCode: 'BIDV',
  bankName:
    'Ngân hàng Đầu tư và Phát triển Việt Nam (BIDV) – Chi nhánh Thái Nguyên',
  accountName: 'Công ty TNHH Truyền thông và Công nghệ KIT',
} as const;

/** Dynamic VietQR image (amount + nội dung mã đơn). */
export function buildKitVietQrUrl(amountVnd: number, addInfo: string): string {
  const content = (addInfo || 'FAMIXA').trim().slice(0, 50);
  const params = new URLSearchParams({
    amount: String(Math.max(0, Math.round(amountVnd))),
    addInfo: content,
    accountName: KIT_BANK_TRANSFER.accountName,
  });
  return `https://img.vietqr.io/image/${KIT_BANK_TRANSFER.bankBin}-${KIT_BANK_TRANSFER.accountNumber}-compact2.png?${params.toString()}`;
}
