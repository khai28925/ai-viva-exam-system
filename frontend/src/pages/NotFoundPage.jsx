import { Link } from 'react-router'

export default function NotFoundPage() {
  return (
    <main className="shell hero">
      <p className="eyebrow">404</p>
      <h1>Không tìm thấy trang</h1>
      <p>
        <Link to="/">Về trang chủ</Link>
      </p>
    </main>
  )
}
