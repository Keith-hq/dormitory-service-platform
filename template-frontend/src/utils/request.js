import axios from 'axios'
import { attachAuthMock } from '@/mock/auth'
import { useUserStore } from '@/store/user'

const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '/api',
  timeout: 10000
})

const AUTH_TOKEN_SNAPSHOT = 'authTokenSnapshot'
let loginRedirectPending = false

export class ApiError extends Error {
  constructor(message, { code, status, cause } = {}) {
    super(message, { cause })
    this.name = 'ApiError'
    this.code = code
    this.status = status
  }
}

const redirectToLogin = (requestToken) => {
  const userStore = useUserStore()

  // 忽略旧请求迟到的 401，避免清除用户刚建立的新会话。
  if (requestToken !== userStore.token) return

  userStore.logout()

  if (window.location.pathname === '/login') return
  if (loginRedirectPending) return

  loginRedirectPending = true
  const currentPath = `${window.location.pathname}${window.location.search}${window.location.hash}`
  const loginUrl = `/login?redirect=${encodeURIComponent(currentPath)}`
  window.location.assign(loginUrl)
}

// 请求拦截器：携带 JWT Token
request.interceptors.request.use(
  (config) => {
    const userStore = useUserStore()
    config[AUTH_TOKEN_SNAPSHOT] = userStore.token
    if (userStore.token) {
      config.headers.Authorization = `Bearer ${userStore.token}`
    }
    return attachAuthMock(config)
  },
  (error) => Promise.reject(error)
)

// 响应拦截器：统一错误处理
request.interceptors.response.use(
  (response) => {
    const payload = response.data
    if (!payload || typeof payload !== 'object' || !('code' in payload)) {
      return Promise.reject(
        new ApiError('接口响应格式不符合约定', {
          code: 'INVALID_RESPONSE',
          status: response.status
        })
      )
    }

    const { code, message, data } = payload
    const normalizedCode = Number(code)

    if (normalizedCode === 200 || normalizedCode === 201) {
      return data
    }

    if (normalizedCode === 401) {
      redirectToLogin(response.config[AUTH_TOKEN_SNAPSHOT])
    }

    return Promise.reject(
      new ApiError(message || '请求失败', {
        code: normalizedCode,
        status: response.status
      })
    )
  },
  (error) => {
    if (error instanceof ApiError) {
      return Promise.reject(error)
    }

    const status = error.response?.status
    if (status === 401) {
      redirectToLogin(error.config?.[AUTH_TOKEN_SNAPSHOT])
    }

    let message = error.response?.data?.message || error.message || '请求失败'
    if (error.code === 'ECONNABORTED') {
      message = '请求超时，请稍后重试'
    } else if (!error.response) {
      message = '网络连接失败，请检查服务是否可用'
    }

    return Promise.reject(
      new ApiError(message, {
        code: error.response?.data?.code || error.code,
        status,
        cause: error
      })
    )
  }
)

export default request
