import request from '@/utils/request'
import { normalizeAuthSession } from '@/utils/authSession'

/**
 * 认证接口。
 * Mock 与真实后端共用同一调用入口，页面不感知数据来源。
 */
export const authApi = {
  login: async (credentials) => {
    const loginResult = await request.post('/auth/login', credentials)

    if (loginResult?.userInfo) {
      return normalizeAuthSession(loginResult)
    }

    const currentUser = await request.get('/auth/me', {
      headers: { Authorization: `Bearer ${loginResult?.token || ''}` }
    })
    return normalizeAuthSession(loginResult, currentUser)
  },
  changePassword: (data) => request.put('/auth/password', data),
  me: () => request.get('/auth/me')
}
