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

## Canonical Checkout API

All products create / poll orders here (do **not** invent per-pack payment UIs):

| Method | Path | Auth | Purpose |
|--------|------|------|---------|
| GET | `/api/payment/plans?productCode=` | Bearer | Plan catalog |
| GET | `/api/payment/methods` | Bearer | Payment methods (VietQR / bank / MoMo…) |
| GET | `/api/payment/subscriptions?productCode=&subjectType=&subjectId=` | Bearer | Entitlement |
| POST | `/api/payment/orders` | Bearer | **Create order** (body: productCode, subjectType, subjectId, planCode?, returnUrl?, cancelUrl?) |
| GET | `/api/payment/orders/{orderCode}?productCode=&subjectId=` | Bearer | Poll status |
| POST | `/api/payment/webhooks/payos` | Anonymous + signature | Provider webhook |
| POST | `/api/payment/orders/activate` | Ops (`payment.ops.activate`) | Manual bank confirm |

Legacy Famixa facade (`POST /api/family-os/families/{id}/billing/checkout`) still works and delegates to the same `IPaymentService`.

## Shared Checkout UI

Deep-link (family-app today; same contract for Novixa/KEMS shells later):

```
/pay?product=family_os&subjectType=family&subjectId={guid}&plan=starter_month&return=/who
```

Optional after create / provider return: `&orderCode={n}`

Flow:

1. Banner / CTA → navigate to `/pay?…`
2. UI loads plans + subscription via `/api/payment/*`
3. User confirms → `POST /api/payment/orders`
4. Prefer PayOS `checkoutUrl`; else show `public_code` + QR and poll until `paid`
5. Webhook / ops activate → subscription extended → product handler syncs entitlement

## Famixa path

1. Parent taps renew on BillingBanner → `/pay?product=family_os&…`
2. Checkout UI → `POST /api/payment/orders`
3. Prefer PayOS VietQR; else pending order with unique `public_code` (e.g. `FMX250725A91`)
4. Webhook `POST /api/payment/webhooks/payos` (fail-closed signature) → extend `payment.subscription` (stacks remaining days) → `FamilyOsPaymentProductHandler` syncs `pack_family.family_subscription`

## Ops activate

`POST /api/payment/orders/activate` and legacy `POST /api/family-os/billing/activate-order` require policy `PaymentOpsActivate` (`payment.ops.activate` or role `PLATFORM_OPS`). Self-serve Famixa ADMIN no longer receives `platform.%` wildcards.

## Config

Prefer `Payment:PayOS:*`. Falls back to `FamilyOs:PayOS:*` if ClientId empty.

Webhook URL for PayOS dashboard: `https://api.novixa.vn/api/payment/webhooks/payos`

## Migration

`224_kit_payment_platform.sql` (listed in `deploy/ubuntu/migration-files.family-os.txt`)
