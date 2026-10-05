import SessionLayout from '../features/auth/SessionLayout.jsx'
import { Link } from 'react-router'

const content = {
  STUDENT: {
    title: 'Không gian sinh viên',
    eyebrow: 'STUDENT WORKSPACE',
    description:
      'Theo dõi lịch thi, tham gia phiên vấn đáp và xem kết quả của bạn.',
    cards: [
      ['Lịch thi', 'Các kỳ thi được phân công sẽ hiển thị tại đây.'],
      ['Kết quả', 'Báo cáo và phản hồi sau buổi thi sẽ hiển thị tại đây.'],
    ],
  },
  LECTURER: {
    title: 'Không gian giảng viên',
    eyebrow: 'LECTURER WORKSPACE',
    description: 'Chuẩn bị nội dung và quản lý các buổi vấn đáp của bạn.',
    cards: [
      [
        'Ngân hàng câu hỏi',
        'Tạo ngân hàng, quản lý câu hỏi và lưu trực tiếp vào PostgreSQL.',
      ],
      ['Phiên thi', 'Lịch thi và phiên vấn đáp sẽ hiển thị tại đây.'],
    ],
  },
}

export default function RoleDashboard({ role }) {
  const details = content[role]
  return (
    <SessionLayout {...details}>
      <div className="auth-cards">
        {details.cards.map(([title, description], index) => (
          <section className="auth-card" key={title}>
            <span>0{index + 1} / MODULE</span>
            <h2>{title}</h2>
            <p>{description}</p>
            {role === 'LECTURER' && index === 0 && (
              <Link
                className="qb-button qb-button--primary"
                to="/question-banks"
              >
                Mở ngân hàng câu hỏi
              </Link>
            )}
          </section>
        ))}
      </div>
      <p className="auth-workspace__notice">
        Các chức năng thi đang được phát triển và sẽ xuất hiện tại đây.
      </p>
    </SessionLayout>
  )
}
