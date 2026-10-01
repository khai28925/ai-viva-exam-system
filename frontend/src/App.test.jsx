import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import App from './App.jsx'

function renderApp(path = '/') {
  return render(
    <MemoryRouter initialEntries={[path]}>
      <App />
    </MemoryRouter>,
  )
}

describe('Application routes and API status', () => {
  it('shows the home page and online API status', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(new Response(JSON.stringify({ status: 'healthy' }))),
    )
    renderApp()
    expect(
      screen.getByRole('heading', { name: /Đánh giá vấn đáp/ }),
    ).toBeInTheDocument()
    expect(await screen.findByText('API online')).toBeInTheDocument()
  })

  it('shows offline status when the API cannot be reached', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockRejectedValue(new TypeError('Failed to fetch')),
    )
    renderApp()
    expect(await screen.findByText('API offline')).toBeInTheDocument()
  })

  it('renders a 404 route and navigates back home', async () => {
    vi.stubGlobal(
      'fetch',
      vi
        .fn()
        .mockResolvedValue(new Response(JSON.stringify({ status: 'healthy' }))),
    )
    renderApp('/missing-page')
    expect(
      screen.getByRole('heading', { name: 'Không tìm thấy trang' }),
    ).toBeInTheDocument()
    await userEvent
      .setup()
      .click(screen.getByRole('link', { name: 'Về trang chủ' }))
    expect(await screen.findByText('API online')).toBeInTheDocument()
    expect(
      screen.getByRole('heading', { name: /Đánh giá vấn đáp/ }),
    ).toBeInTheDocument()
  })
})
