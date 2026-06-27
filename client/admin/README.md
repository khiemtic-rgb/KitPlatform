# PharmaCore Admin Web

React + Vite + TypeScript + Ant Design — ERP Admin (Phase 1).

## Yêu cầu

- Node.js 20+ LTS
- API đang chạy: `http://localhost:5290`

## Chạy

```bash
cd client/admin
npm install
npm run dev
```

Mở **http://localhost:5173** — login: `admin` / `Admin@123`

Hoặc từ root project:

```bat
run-dev.bat
```

Checklist demo POS: [demo-pos-checklist.md](./demo-pos-checklist.md)

Checklist demo Mua hàng: [demo-procurement-checklist.md](./demo-procurement-checklist.md)

Checklist Customer App: [demo-customer-app-checklist.md](../customer-app/demo-customer-app-checklist.md)

Phạm vi Giai đoạn 1 / 2 (feature flags): [PHASE_SCOPE.md](./PHASE_SCOPE.md)

Báo cáo Wave 1: [REPORTS_WAVE1.md](./REPORTS_WAVE1.md)

## OpenAPI / types (FE ↔ BE)

Sau khi đổi API backend:

```bash
cd client/admin
npm run openapi:sync    # export swagger.json + sinh TypeScript
```

- Spec: `openapi/swagger.json`
- Types: `src/shared/api/generated/api-schema.ts` (không sửa tay)
- Re-export: `src/shared/api/generated/index.ts`

Script offline: `scripts/export-openapi.ps1` (fallback: tải từ API đang chạy `:5290`).

## Cấu trúc module

```
src/
├── app/           # router, providers
├── modules/       # auth, dashboard, (catalog, inventory, ...)
├── shared/        # api, auth, components
└── styles/
```

Bật module trong `src/modules/registry.tsx` (`enabled: true`) khi API sẵn sàng.
