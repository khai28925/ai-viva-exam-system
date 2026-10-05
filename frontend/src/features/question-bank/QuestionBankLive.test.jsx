import { env } from 'node:process'
import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router'
import { expect, it, vi } from 'vitest'
import App from '../../App.jsx'
import {
  deleteQuestion,
  deleteQuestionBank,
  getQuestions,
} from '../../api/questionBankApi.js'

// Opt-in only. The PowerShell smoke harness supplies an isolated database/API
// and ephemeral credentials, never the developer's existing application data.
it.skipIf(env.AIVES_LIVE_CRUD !== '1')(
  'runs the actual React CRUD forms against the isolated API and PostgreSQL',
  async () => {
    const apiBase = env.VITE_API_BASE_URL
    const target = new URL(apiBase)
    expect(['localhost', '127.0.0.1', '[::1]']).toContain(target.hostname)
    expect(env.AIVES_TEST_EMAIL).toBeTruthy()
    expect(env.AIVES_TEST_PASSWORD).toBeTruthy()
    const nativeFetch = globalThis.fetch
    const cookies = new Map()
    let createdBankId = null
    let primaryError
    let cleanupError

    // Node's HTTP fetch does not have a browser cookie jar. This test-only jar
    // forwards cookies returned by this isolated API; no browser/session stores.
    vi.stubGlobal('fetch', async (path, options = {}) => {
      const requestUrl = new URL(path, apiBase)
      if (requestUrl.origin !== target.origin)
        throw new Error('Live test cannot call another origin.')
      const headers = new Headers(options.headers)
      if (cookies.size)
        headers.set(
          'Cookie',
          [...cookies].map(([key, value]) => `${key}=${value}`).join('; '),
        )
      const response = await nativeFetch(requestUrl, {
        ...options,
        headers,
        redirect: 'error',
      })
      for (const cookie of response.headers.getSetCookie()) {
        const pair = cookie.split(';', 1)[0]
        const split = pair.indexOf('=')
        const key = pair.slice(0, split)
        const value = pair.slice(split + 1)
        if (/max-age=0(?:;|$)/i.test(cookie) || !value) cookies.delete(key)
        else cookies.set(key, value)
      }
      if (
        requestUrl.pathname === '/api/v1/question-banks' &&
        options.method === 'POST' &&
        response.status === 201
      ) {
        createdBankId = (await response.clone().json()).id
      }
      return response
    })

    function mount() {
      return render(
        <MemoryRouter initialEntries={['/question-banks']}>
          <App />
        </MemoryRouter>,
      )
    }
    async function waitForDialogToClose() {
      await waitFor(
        () => expect(screen.queryByRole('dialog')).not.toBeInTheDocument(),
        { timeout: 10000 },
      )
    }
    function questionList() {
      return within(
        screen.getByRole('region', { name: 'Câu hỏi trong ngân hàng' }),
      )
    }
    let view = mount()
    const user = userEvent.setup()
    const name = `React live ${Date.now()}`
    const updatedName = `${name} updated`
    try {
      await screen.findByRole('heading', { name: 'Đăng nhập.' })
      await user.type(screen.getByLabelText('Email'), env.AIVES_TEST_EMAIL)
      await user.type(
        screen.getByLabelText('Mật khẩu'),
        env.AIVES_TEST_PASSWORD,
      )
      await user.click(screen.getByRole('button', { name: /Đăng nhập/ }))
      await screen.findByRole('button', { name: 'Thêm ngân hàng', exact: true })
      await user.click(
        screen.getByRole('button', { name: 'Thêm ngân hàng', exact: true }),
      )
      let dialog = screen.getByRole('dialog', { name: 'Thêm ngân hàng' })
      await user.type(
        within(dialog).getByRole('textbox', { name: /Tên ngân hàng/ }),
        name,
      )
      await user.type(
        within(dialog).getByRole('textbox', { name: /Mô tả/ }),
        'Created by React against PostgreSQL',
      )
      await user.click(
        within(dialog).getByRole('button', {
          name: 'Thêm ngân hàng',
          exact: true,
        }),
      )
      await waitForDialogToClose()
      expect(
        await screen.findByRole('heading', { name, exact: true }),
      ).toBeInTheDocument()
      expect(createdBankId).toMatch(/^[0-9a-f-]{36}$/i)

      await user.click(
        screen.getByRole('button', { name: 'Thêm câu hỏi', exact: true }),
      )
      dialog = screen.getByRole('dialog', { name: 'Thêm câu hỏi' })
      await user.type(
        within(dialog).getByRole('textbox', { name: /Nội dung câu hỏi/ }),
        'What does dependency inversion mean?',
      )
      await user.click(
        within(dialog).getByRole('button', {
          name: 'Thêm câu hỏi',
          exact: true,
        }),
      )
      await waitForDialogToClose()
      expect(
        questionList().getByText('What does dependency inversion mean?', {
          selector: 'p',
        }),
      ).toBeInTheDocument()
      await user.click(screen.getByRole('button', { name: 'Sửa câu hỏi 1' }))
      dialog = screen.getByRole('dialog', { name: 'Chỉnh sửa câu hỏi' })
      const content = within(dialog).getByRole('textbox', {
        name: /Nội dung câu hỏi/,
      })
      await user.clear(content)
      await user.type(content, 'Explain dependency inversion with an example.')
      await user.click(
        within(dialog).getByRole('button', { name: 'Lưu thay đổi' }),
      )
      await waitForDialogToClose()
      expect(
        questionList().getByText(
          'Explain dependency inversion with an example.',
          { selector: 'p' },
        ),
      ).toBeInTheDocument()

      await user.click(
        screen.getByRole('button', { name: 'Sửa ngân hàng', exact: true }),
      )
      dialog = screen.getByRole('dialog', { name: 'Chỉnh sửa ngân hàng' })
      const bankName = within(dialog).getByRole('textbox', {
        name: /Tên ngân hàng/,
      })
      await user.clear(bankName)
      await user.type(bankName, updatedName)
      await user.click(
        within(dialog).getByRole('button', { name: 'Lưu thay đổi' }),
      )
      await waitForDialogToClose()
      await screen.findByRole('heading', { name: updatedName, exact: true })

      // Remount the entire application: data must be fetched, not retained in React state.
      view.unmount()
      view = mount()
      await user.click(
        await screen.findByRole('button', { name: new RegExp(updatedName) }),
      )
      expect(
        questionList().getByText(
          'Explain dependency inversion with an example.',
          { selector: 'p' },
        ),
      ).toBeInTheDocument()

      await user.click(
        screen.getByRole('button', { name: 'Xóa ngân hàng', exact: true }),
      )
      dialog = screen.getByRole('dialog', { name: 'Xóa ngân hàng?' })
      await user.click(
        within(dialog).getByRole('button', {
          name: 'Xóa ngân hàng',
          exact: true,
        }),
      )
      expect(
        await within(dialog).findByText(/Ngân hàng vẫn còn câu hỏi/),
      ).toBeInTheDocument()
      await user.click(within(dialog).getByRole('button', { name: 'Giữ lại' }))

      await user.click(screen.getByRole('button', { name: 'Xóa câu hỏi 1' }))
      await user.click(
        within(screen.getByRole('dialog')).getByRole('button', {
          name: 'Xóa câu hỏi',
          exact: true,
        }),
      )
      await waitForDialogToClose()
      expect(await screen.findByText('Chưa có câu hỏi nào')).toBeInTheDocument()
      await user.click(
        screen.getByRole('button', { name: 'Xóa ngân hàng', exact: true }),
      )
      await user.click(
        within(screen.getByRole('dialog')).getByRole('button', {
          name: 'Xóa ngân hàng',
          exact: true,
        }),
      )
      await waitForDialogToClose()
      expect(
        screen.queryByRole('button', { name: new RegExp(updatedName) }),
      ).not.toBeInTheDocument()
      createdBankId = null
      await user.click(screen.getByRole('button', { name: 'Làm mới dữ liệu' }))
      await waitFor(() =>
        expect(
          screen.queryByText('Đang tải ngân hàng câu hỏi'),
        ).not.toBeInTheDocument(),
      )
      expect(
        screen.queryByRole('button', { name: new RegExp(updatedName) }),
      ).not.toBeInTheDocument()
    } catch (error) {
      primaryError = error
    } finally {
      view.unmount()
      if (createdBankId) {
        try {
          const remaining = await getQuestions(createdBankId)
          for (const item of remaining)
            await deleteQuestion(createdBankId, item.id)
          await deleteQuestionBank(createdBankId)
        } catch (error) {
          // A completed DELETE can leave no data to clean. Never replace the
          // original test failure with a secondary cleanup/network failure.
          if (error.status !== 404) cleanupError = error
        }
      }
    }
    if (primaryError) throw primaryError
    if (cleanupError) throw cleanupError
  },
  60000,
)
