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

## Cấu trúc module

```
src/
├── app/           # router, providers
├── modules/       # auth, dashboard, (catalog, inventory, ...)
├── shared/        # api, auth, components
└── styles/
```

Bật module trong `src/modules/registry.tsx` (`enabled: true`) khi API sẵn sàng.
