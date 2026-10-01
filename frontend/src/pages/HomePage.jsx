import { useEffect, useState } from 'react'
import { Link } from 'react-router'
import { getHealth } from '../api/healthApi.js'

const modules = [
  ['Question Bank', 'Quản lý ngân hàng câu hỏi và rubric chấm điểm.'],
  ['Viva Session', 'Điều phối phiên thi và câu hỏi probing thích ứng.'],
  ['Assessment', 'Tổng hợp transcript, điểm số và phản hồi sau kỳ thi.'],
]

function HomePage() {
  const [apiStatus, setApiStatus] = useState('checking')

  useEffect(() => {
    const controller = new AbortController()

    getHealth(controller.signal)
      .then(() => setApiStatus('online'))
      .catch((error) => {
        if (error.name !== 'AbortError') setApiStatus('offline')
      })

    return () => controller.abort()
  }, [])

  return (
    <main className="shell">
      <nav className="nav">
        <Link className="brand" to="/">
          VIVA<span>AI</span>
        </Link>
        <div className={`status status--${apiStatus}`}>
          <span className="status__dot" />
          API {apiStatus}
        </div>
      </nav>

      <section className="hero">
        <p className="eyebrow">AI-powered oral examination</p>
        <h1>
          Đánh giá vấn đáp
          <br />
          rõ ràng và nhất quán.
        </h1>
        <p className="hero__copy">
          Nền tảng hỗ trợ chuẩn bị câu hỏi, tổ chức phiên viva thích ứng và tạo
          báo cáo phản hồi có cấu trúc.
        </p>
      </section>

      <section className="modules" aria-label="Các module chính">
        {modules.map(([title, description], index) => (
          <article className="module" key={title}>
            <span className="module__number">0{index + 1}</span>
            <h2>{title}</h2>
            <p>{description}</p>
          </article>
        ))}
      </section>
    </main>
  )
}

export default HomePage
