# Database migrations

SQL migrations for PharmaCore PostgreSQL schema. Run in numeric order.

## First-time setup (empty PostgreSQL)

```powershell
.\scripts\setup-and-migrate.ps1 -PostgresPassword <postgres_superuser_password>
```

Creates database user, runs `001_extensions.sql` as superuser, then applies `002`–`018` and seeds.

## Existing database (Docker / dev)

```powershell
docker compose up -d
.\scripts\run-migrations.ps1
```

**Note:** `run-migrations.ps1` includes `001_extensions.sql`. On an existing DB that already has extensions, run only missing files or use `setup-and-migrate` on a fresh instance.

## File order

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
| seed/* | Demo tenant, admin password, sample customers |

Keep `run-migrations.ps1` and `setup-and-migrate.ps1` in sync when adding new files.
