# Frontend

React SPA được khởi tạo bằng Vite.

## Chạy local

```bash
npm ci
npm run dev
```

Ứng dụng mặc định chạy tại `http://localhost:5173` và proxy request `/api`
sang backend tại `http://localhost:5065`.

Sao chép `.env.example` thành `.env` khi cần đổi API base URL.

## Công cụ và quy ước

- Node.js `22.19.0`: `.nvmrc` và `.node-version` nằm ở repository root.
- React Router: route ở `src/App.jsx`, page ở `src/pages/`.
  Route `/` là trang chủ, route `*` là trang 404.
- ESLint flat config kiểm tra JavaScript, JSX và React Hooks.
- Prettier đồng bộ format; chạy `npm run format` và `npm run format:check`.
- Vitest + Testing Library: test đặt cạnh module (`*.test.js`, `*.test.jsx`).
  Chạy `npm test` hoặc `npm run test:watch`.
- `npm run check`: lint → format check → test → build.
- HTTP client giữ Problem Details trong `ApiError.problem`, bao gồm `errors`
  và `traceId` để form CRUD có thể hiển thị lỗi theo field.

Production cần cấu hình web server trả `index.html` cho route frontend không
trùng static asset/API, để reload đường dẫn hoạt động với `BrowserRouter`.
Proxy `/api` trong Vite chỉ dùng cho local development; production cần reverse
proxy `/api` hoặc `VITE_API_BASE_URL` phù hợp.
