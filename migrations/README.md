# Database migrations

SQL migrations for PharmaCore PostgreSQL schema. Run in numeric order.

## First-time setup (empty PostgreSQL) — Dev / demo

```powershell
.\scripts\setup-and-migrate.ps1 -PostgresPassword <postgres_superuser_password>
```

Creates database user, runs `001_extensions.sql` as superuser, then applies schema + **demo seed**.

## Existing database (Docker / dev)

```powershell
docker compose up -d
.\scripts\run-migrations.ps1
```

**Note:** `run-migrations.ps1` includes demo seeds and sample loyalty/CDP data. Do **not** use it on Production.

## Production (pilot / go-live) — no demo seed

```powershell
.\scripts\run-migrations-prod.ps1 -ConnectionString "postgresql://user:pass@host:5432/pharmacore_nt_a"
.\scripts\bootstrap-first-tenant.ps1 -ConnectionString "..." -TenantCode NT_A -TenantName "..." -AdminEmail "..." -AdminPassword "..."
```

See [pilot-go-live-checklist.md](../client/admin/pilot-go-live-checklist.md).

## File order (schema)

| File | Module |
|------|--------|
| 001_extensions.sql | PostgreSQL extensions (superuser) |
| 002_identity.sql | Auth, tenants, RBAC |
| 003_catalog.sql | Products, units, prices, barcodes |
| 004_inventory.sql | Warehouses, batches, movements |
| 005_procurement.sql | PO, GRN, suppliers |
| 006_sales.sql | Sales orders |
| 007_customer_app.sql | Loyalty, reminders |
| 008–011 | Images, search, procurement status, v2 readiness |
| 012–018 | Sales drafts, discounts, returns, shifts, CDP outbox, batch source |
| 019–040 | OTP, loyalty schema, customer app, inventory count, reports permissions, national drug link |
| 039_reports_permissions.sql | `reports.read`, `reports.export` |
| seed/* | **Dev only** — demo tenant, admin password, sample customers |
| seed-prod/001_base_permissions.sql | **Prod** — global permission catalog (no tenant) |

**Demo-only migrations** (in `run-migrations.ps1` only): `020`, `021`, `027`, `032`, `seed/*`.

Keep `run-migrations.ps1`, `run-migrations-prod.ps1`, and `setup-and-migrate.ps1` in sync when adding new schema files.
