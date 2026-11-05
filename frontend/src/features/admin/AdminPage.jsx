import { useEffect, useState } from 'react'
import { createUser, getUsers, updateUserProfile } from './adminApi.js'
import { useAuth } from '../auth/authContext.js'
import SessionLayout from '../auth/SessionLayout.jsx'

const roleLabels = {
  ADMIN: 'Quản trị viên',
  LECTURER: 'Giảng viên',
  STUDENT: 'Sinh viên',
}

// Optional profile fields are only sent when filled so the account contract stays minimal.
function profileFields(fullName, studentCode) {
  return {
    ...(fullName.trim() ? { fullName: fullName.trim() } : {}),
    ...(studentCode.trim() ? { studentCode: studentCode.trim() } : {}),
  }
}

function ProfileEditor({ account, onSaved, onCancel, onError }) {
  const [fullName, setFullName] = useState(account.fullName ?? '')
  const [studentCode, setStudentCode] = useState(account.studentCode ?? '')
  const [pending, setPending] = useState(false)

  async function save(event) {
    event.preventDefault()
    setPending(true)
    try {
      onSaved(
        await updateUserProfile(account.id, {
          fullName: fullName.trim() || null,
          studentCode: studentCode.trim() || null,
        }),
      )
    } catch (error) {
      onError(error)
    } finally {
      setPending(false)
    }
  }

  return (
    <form className="auth-admin-profile" onSubmit={save}>
      <label htmlFor={`profile-name-${account.id}`}>Họ và tên</label>
      <input
        id={`profile-name-${account.id}`}
        maxLength={160}
        value={fullName}
        onChange={(event) => setFullName(event.target.value)}
      />
      {account.role === 'STUDENT' && (
        <>
          <label htmlFor={`profile-code-${account.id}`}>MSSV</label>
          <input
            id={`profile-code-${account.id}`}
            maxLength={32}
            value={studentCode}
            onChange={(event) => setStudentCode(event.target.value)}
          />
        </>
      )}
      <span>
        <button type="submit" disabled={pending}>
          {pending ? 'Đang lưu...' : 'Lưu hồ sơ'}
        </button>
        <button type="button" onClick={onCancel} disabled={pending}>
          Hủy
        </button>
      </span>
    </form>
  )
}

export default function AdminPage() {
  const { invalidate } = useAuth()
  const [users, setUsers] = useState([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState('')
  const [formError, setFormError] = useState('')
  const [success, setSuccess] = useState('')
  const [pending, setPending] = useState(false)
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] = useState('STUDENT')
  const [fullName, setFullName] = useState('')
  const [studentCode, setStudentCode] = useState('')
  const [editingId, setEditingId] = useState(null)

  useEffect(() => {
    const controller = new AbortController()
    getUsers(controller.signal)
      .then((accounts) => {
        if (!controller.signal.aborted) setUsers(accounts)
      })
      .catch((error) => {
        if (controller.signal.aborted || error.name === 'AbortError') return
        if (error.status === 401) invalidate()
        else setLoadError('Không thể tải danh sách tài khoản.')
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [invalidate])

  async function handleCreate(event) {
    event.preventDefault()
    setFormError('')
    setSuccess('')
    setPending(true)
    try {
      const created = await createUser({
        email: email.trim(),
        password,
        role,
        ...profileFields(fullName, role === 'STUDENT' ? studentCode : ''),
      })
      setUsers((current) => [...current, created])
      setEmail('')
      setPassword('')
      setRole('STUDENT')
      setFullName('')
      setStudentCode('')
      setSuccess(`Đã tạo tài khoản ${created.email}.`)
    } catch (error) {
      if (error.status === 401) invalidate()
      else {
        setFormError(
          error.problem?.detail ??
            (error.status === 409
              ? 'Email này đã được sử dụng.'
              : 'Không thể tạo tài khoản. Vui lòng thử lại.'),
        )
      }
    } finally {
      setPending(false)
    }
  }

  return (
    <SessionLayout
      title="Quản lý tài khoản"
      eyebrow="ADMIN WORKSPACE / USERS"
      description="Cấp tài khoản giảng viên và sinh viên cho hệ thống."
    >
      <div className="auth-admin-grid">
        <section className="auth-card auth-admin-form">
          <span>01 / TẠO TÀI KHOẢN</span>
          <h2>Thành viên mới</h2>
          <p>
            Cấp tài khoản cho giảng viên hoặc sinh viên với vai trò phù hợp. Họ
            tên và MSSV được in trên bảng điểm xuất ra.
          </p>
          <form onSubmit={handleCreate}>
            <label htmlFor="admin-email">Email</label>
            <input
              id="admin-email"
              type="email"
              autoComplete="off"
              required
              maxLength={254}
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              placeholder="name@example.com"
            />
            <label htmlFor="admin-password">Mật khẩu ban đầu</label>
            <input
              id="admin-password"
              type="password"
              autoComplete="new-password"
              required
              minLength={12}
              maxLength={128}
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              placeholder="Nhập mật khẩu"
            />
            <label htmlFor="admin-role">Vai trò</label>
            <select
              id="admin-role"
              value={role}
              onChange={(event) => setRole(event.target.value)}
            >
              <option value="STUDENT">Sinh viên</option>
              <option value="LECTURER">Giảng viên</option>
            </select>
            <label htmlFor="admin-full-name">Họ và tên</label>
            <input
              id="admin-full-name"
              autoComplete="off"
              maxLength={160}
              value={fullName}
              onChange={(event) => setFullName(event.target.value)}
              placeholder="Nguyễn Văn An"
            />
            {role === 'STUDENT' && (
              <>
                <label htmlFor="admin-student-code">MSSV</label>
                <input
                  id="admin-student-code"
                  autoComplete="off"
                  maxLength={32}
                  value={studentCode}
                  onChange={(event) => setStudentCode(event.target.value)}
                  placeholder="SE180001"
                />
              </>
            )}
            {formError && (
              <p className="auth-form-error" role="alert">
                {formError}
              </p>
            )}
            {success && (
              <p className="auth-form-success" role="status">
                {success}
              </p>
            )}
            <button type="submit" disabled={pending}>
              {pending ? 'Đang tạo...' : 'Tạo tài khoản'}
            </button>
          </form>
        </section>

        <section className="auth-card auth-admin-list">
          <span>02 / DANH SÁCH</span>
          <div className="auth-admin-list__title">
            <h2>Tài khoản hiện có</h2>
            <strong>{users.length}</strong>
          </div>
          {loading ? (
            <p role="status">Đang tải tài khoản...</p>
          ) : loadError ? (
            <p role="alert">{loadError}</p>
          ) : users.length === 0 ? (
            <p>Chưa có tài khoản nào.</p>
          ) : (
            <ul>
              {users.map((account) => (
                <li key={account.id}>
                  <span className="auth-admin-list__avatar" aria-hidden="true">
                    {(account.fullName || account.email)
                      .charAt(0)
                      .toUpperCase()}
                  </span>
                  <span className="auth-admin-list__identity">
                    <strong>{account.fullName || account.email}</strong>
                    <small>
                      {roleLabels[account.role] ?? account.role}
                      {account.fullName && ` · ${account.email}`}
                      {account.studentCode && ` · MSSV ${account.studentCode}`}
                    </small>
                    {editingId === account.id && (
                      <ProfileEditor
                        account={account}
                        onCancel={() => setEditingId(null)}
                        onError={(error) => {
                          if (error.status === 401) invalidate()
                          else
                            setFormError('Không thể cập nhật hồ sơ tài khoản.')
                        }}
                        onSaved={(updated) => {
                          setUsers((current) =>
                            current.map((item) =>
                              item.id === updated.id ? updated : item,
                            ),
                          )
                          setEditingId(null)
                          setSuccess(`Đã cập nhật hồ sơ ${updated.email}.`)
                        }}
                      />
                    )}
                  </span>
                  {account.role !== 'ADMIN' && editingId !== account.id && (
                    <button
                      type="button"
                      className="auth-admin-list__edit"
                      aria-label={`Sửa hồ sơ ${account.email}`}
                      onClick={() => setEditingId(account.id)}
                    >
                      Sửa
                    </button>
                  )}
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </SessionLayout>
  )
}
