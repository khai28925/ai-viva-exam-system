import SessionLayout from '../features/auth/SessionLayout.jsx'
import { Link } from 'react-router'

const content = {
  STUDENT: {
    title: 'Không gian sinh viên',
    eyebrow: 'STUDENT WORKSPACE',
    description:
      'Xem các phiên đang mở và vào xem câu hỏi do giảng viên chuẩn bị.',
    cards: [
      [
        'Phiên thi',
        'Vào phiên đang mở để xem câu hỏi. Lượt tham gia được lưu vào hệ thống.',
      ],
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
      [
        'Phiên thi',
        'Tạo, sửa, xóa phiên thi và mở phiên cho sinh viên tham gia.',
      ],
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
            {((role === 'LECTURER' && index === 1) ||
              (role === 'STUDENT' && index === 0)) && (
              <Link
                className="exam-button exam-button--primary"
                to="/exam-sessions"
              >
                {role === 'STUDENT'
                  ? 'Xem phiên thi và vào thi'
                  : 'Quản lý phiên thi'}
              </Link>
            )}
          </section>
        ))}
      </div>
      <p className="auth-workspace__notice">
        Phiên thi hiện ở mức cơ bản: chưa có nộp bài, chấm điểm, AI hoặc ghi âm.
      </p>
    </SessionLayout>
  )
}
