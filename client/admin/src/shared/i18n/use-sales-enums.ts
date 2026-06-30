import { useMemo } from 'react';

import { useTranslation } from 'react-i18next';



const PAYMENT_METHOD_IDS = [1, 2, 3, 4, 5] as const;

const COLLECTION_PAYMENT_METHOD_IDS = [1, 2, 3, 4] as const;

const CUSTOMER_DRAFT_STATUS_IDS = [1, 2, 3, 4, 5, 6] as const;

const RETURN_STATUS_IDS = [2] as const;

const CUSTOMER_PAYMENT_STATUS_IDS = [1, 2, 3] as const;

const RESERVATION_STATUS_IDS = [1, 2, 3, 4, 5, 6] as const;

const VOUCHER_STATUS_IDS = [0, 1] as const;

const VOUCHER_DISCOUNT_TYPE_IDS = [1, 2] as const;



export function useSalesEnums() {

  const { t } = useTranslation('sales');



  const paymentMethodLabel = (method: number) =>

    t(`enums.paymentMethod.${method}`, { defaultValue: String(method) });



  const customerDraftStatusLabel = (status: number) =>

    t(`enums.customerDraftStatus.${status}`, { defaultValue: String(status) });



  const returnStatusLabel = (status: number) =>

    t(`enums.returnStatus.${status}`, { defaultValue: String(status) });



  const customerPaymentStatusLabel = (status: number) =>

    t(`enums.customerPaymentStatus.${status}`, { defaultValue: String(status) });



  const reservationStatusLabel = (status: number) =>

    t(`enums.reservationStatus.${status}`, { defaultValue: String(status) });



  const reservationFulfillmentLabel = (type: number) =>

    t(`enums.reservationFulfillment.${type}`, { defaultValue: String(type) });



  const voucherStatusLabel = (status: number) =>

    t(`enums.voucherStatus.${status}`, { defaultValue: String(status) });



  const voucherDiscountTypeLabel = (type: number) =>

    t(`enums.voucherDiscountType.${type}`, { defaultValue: String(type) });



  const consentChannelLabel = (channel: number) =>

    t(`enums.consentChannel.${channel}`, { defaultValue: String(channel) });



  const consentPurposeLabel = (purpose: number) =>

    t(`enums.consentPurpose.${purpose}`, { defaultValue: String(purpose) });



  const paymentMethodOptions = useMemo(

    () => PAYMENT_METHOD_IDS.map((value) => ({ value, label: paymentMethodLabel(value) })),

    [t],

  );



  const collectionPaymentMethodOptions = useMemo(

    () =>

      COLLECTION_PAYMENT_METHOD_IDS.map((value) => ({

        value,

        label: paymentMethodLabel(value),

      })),

    [t],

  );



  const customerDraftStatusOptions = useMemo(

    () => CUSTOMER_DRAFT_STATUS_IDS.map((value) => ({ value, label: customerDraftStatusLabel(value) })),

    [t],

  );



  const returnStatusOptions = useMemo(

    () => RETURN_STATUS_IDS.map((value) => ({ value, label: returnStatusLabel(value) })),

    [t],

  );



  const customerPaymentStatusOptions = useMemo(

    () =>

      CUSTOMER_PAYMENT_STATUS_IDS.map((value) => ({

        value,

        label: customerPaymentStatusLabel(value),

      })),

    [t],

  );



  const reservationStatusOptions = useMemo(

    () =>

      RESERVATION_STATUS_IDS.map((value) => ({

        value,

        label: reservationStatusLabel(value),

      })),

    [t],

  );



  const voucherStatusOptions = useMemo(

    () =>

      VOUCHER_STATUS_IDS.map((value) => ({

        value,

        label: voucherStatusLabel(value),

      })),

    [t],

  );



  const voucherDiscountTypeOptions = useMemo(

    () =>

      VOUCHER_DISCOUNT_TYPE_IDS.map((value) => ({

        value,

        label: voucherDiscountTypeLabel(value),

      })),

    [t],

  );



  return {

    paymentMethodLabel,

    paymentMethodOptions,

    collectionPaymentMethodOptions,

    customerDraftStatusLabel,

    customerDraftStatusOptions,

    returnStatusLabel,

    returnStatusOptions,

    customerPaymentStatusLabel,

    customerPaymentStatusOptions,

    reservationStatusLabel,

    reservationStatusOptions,

    reservationFulfillmentLabel,

    voucherStatusLabel,

    voucherStatusOptions,

    voucherDiscountTypeLabel,

    voucherDiscountTypeOptions,

    consentChannelLabel,

    consentPurposeLabel,

  };

}

