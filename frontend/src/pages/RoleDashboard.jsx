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
      ['Kết quả', 'Xem điểm từng câu, phản hồi AI và điểm giảng viên đã chốt.'],
    ],
  },
  LECTURER: {
    title: 'Không gian giảng viên',
    eyebrow: 'LECTURER WORKSPACE',
    description: 'Chuẩn bị nội dung và quản lý các buổi vấn đáp của bạn.',
    cards: [
      [
        'Bộ câu hỏi',
        'Tạo bộ câu hỏi, quản lý câu hỏi và lưu trực tiếp vào PostgreSQL.',
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
              <div className="wf1-actions">
                <Link
                  className="qb-button qb-button--primary"
                  to="/wf1-preparation"
                >
                  Chuẩn bị câu hỏi bằng AI
                </Link>
                <Link className="qb-button" to="/question-banks">
                  Quản lý bộ câu hỏi
                </Link>
              </div>
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
            {role === 'STUDENT' && index === 1 && (
              <Link className="exam-button exam-button--primary" to="/reports">
                Xem bảng điểm
              </Link>
            )}
          </section>
        ))}
      </div>
      <p className="auth-workspace__notice">
        WF1 hiện hỗ trợ tự động tạo bản nháp câu hỏi và rubric từ PDF; giảng
        viên luôn là người duyệt nội dung trước khi sử dụng.
      </p>
    </SessionLayout>
  )
}
