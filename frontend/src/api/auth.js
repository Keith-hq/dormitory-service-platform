import request from '@/utils/request'

/**
 * 认证接口。
 * Mock 与真实后端共用同一调用入口，页面不感知数据来源。
 */
export const authApi = {
  login: (credentials) => request.post('/auth/login', credentials)
}
