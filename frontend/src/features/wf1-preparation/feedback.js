export function describePreparationError(error, fallback) {
  const status = error?.status ?? error?.problem?.status
  const code = error?.problem?.code
  if (status === 401)
    return 'Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.'
  if (status === 403) return 'Bạn không có quyền thực hiện thao tác này.'
  if (status === 404) return 'Dữ liệu không còn tồn tại. Hãy tải lại màn hình.'
  if (status === 409 && code === 'question_preparation_locked')
    return 'Câu hỏi đã thuộc phiên Open hoặc Closed nên không thể sửa ý chính.'
  if (status === 409 && code === 'rubric_preparation_locked')
    return 'Ngân hàng đã được dùng trong phiên Open hoặc Closed nên không thể sửa rubric.'
  if (status === 409 && code === 'session_not_ready')
    return 'Phiên chưa đủ điều kiện mở. Kiểm tra các mục còn thiếu bên dưới.'
  if (status === 409)
    return (
      error?.problem?.detail || 'Dữ liệu đã thay đổi. Hãy tải lại rồi thử lại.'
    )

  const fieldErrors = Object.values(error?.problem?.errors ?? {})
    .flat()
    .filter((message) => typeof message === 'string')
  return fieldErrors.join(' ') || error?.problem?.detail || fallback
}

export function validateContent(content) {
  const normalized = content.trim()
  return normalized.length >= 1 && normalized.length <= 2000
    ? null
    : 'Nội dung cần từ 1 đến 2000 ký tự.'
}
