# Kit Payment Platform — foundation v1

Shared SaaS billing for Famixa / Novixa / KEMS. Products plug in via `IPaymentProductHandler`; gateways plug in via `IPaymentProvider`.

## Layout

```
payment.plan
payment.subscription          ← core (trial / active / past_due / expired)
payment.payment_order         ← checkout (order_code + public_code FMX…)
payment.payment_transaction   ← webhook / provider audit

IPaymentService
  └── IPaymentProvider (PayOS now; VNPay/MoMo/ZaloPay later)
  └── IPaymentProductHandler (FamilyOs now; Novixa later)
```

## Famixa path

1. Parent taps renew → `POST /api/family-os/families/{id}/billing/checkout`
2. Facade → `IPaymentService.CreateOrderAsync(product=family_os, subject=family)`
3. Prefer PayOS VietQR; else pending order with unique `public_code` (e.g. `FMX250725A91`)
4. Webhook `POST /api/payment/webhooks/payos` (fail-closed signature) → extend `payment.subscription` (stacks remaining days) → `FamilyOsPaymentProductHandler` syncs `pack_family.family_subscription`

## Ops activate

`POST /api/payment/orders/activate` and legacy `POST /api/family-os/billing/activate-order` require policy `PaymentOpsActivate` (`payment.ops.activate` or role `PLATFORM_OPS`). Self-serve Famixa ADMIN no longer receives `platform.%` wildcards.

## Config

Prefer `Payment:PayOS:*`. Falls back to `FamilyOs:PayOS:*` if ClientId empty.

Webhook URL for PayOS dashboard: `https://api.novixa.vn/api/payment/webhooks/payos`

## Migration

`224_kit_payment_platform.sql` (listed in `deploy/ubuntu/migration-files.family-os.txt`)
