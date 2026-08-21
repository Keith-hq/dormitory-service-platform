import { defineStore } from 'pinia'

const TOKEN_KEY = 'token'
const USER_INFO_KEY = 'userInfo'
const SUPPORTED_ROLES = new Set(['student', 'admin', 'repairman', 'counselor', 'super_admin'])

const getLocalStorage = () => {
  try {
    return window.localStorage
  } catch {
    return null
  }
}

const safeGetItem = (key) => {
  try {
    return getLocalStorage()?.getItem(key) ?? null
  } catch {
    return null
  }
}

const safeSetItem = (key, value) => {
  try {
    const storage = getLocalStorage()
    if (!storage) return false
    storage.setItem(key, value)
    return true
  } catch {
    return false
  }
}

const safeRemoveItem = (key) => {
  try {
    getLocalStorage()?.removeItem(key)
  } catch {
    // 存储不可用时，Pinia 中的内存状态仍可正常清理。
  }
}

const clearStoredSession = () => {
  safeRemoveItem(TOKEN_KEY)
  safeRemoveItem(USER_INFO_KEY)
}

const isValidUserInfo = (userInfo) => {
  if (!userInfo || typeof userInfo !== 'object' || Array.isArray(userInfo)) return false

  const hasRequiredFields = ['id', 'name', 'role'].every(
    (key) => typeof userInfo[key] === 'string' && userInfo[key].trim().length > 0
  )

  return hasRequiredFields && SUPPORTED_ROLES.has(userInfo.role)
}

const readStoredSession = () => {
  const token = safeGetItem(TOKEN_KEY)
  const storedUserInfo = safeGetItem(USER_INFO_KEY)

  if (!token || !storedUserInfo) {
    clearStoredSession()
    return { token: '', userInfo: null }
  }

  try {
    const userInfo = JSON.parse(storedUserInfo)
    if (!isValidUserInfo(userInfo)) {
      throw new TypeError('用户信息格式无效')
    }
    return { token, userInfo }
  } catch {
    clearStoredSession()
    return { token: '', userInfo: null }
  }
}

/**
 * 用户状态仓库
 */
export const useUserStore = defineStore('user', {
  state: () => readStoredSession(),

  getters: {
    isLoggedIn: (state) => Boolean(state.token && state.userInfo),
    userName: (state) => state.userInfo?.name || ''
  },

  actions: {
    setSession({ token, userInfo }) {
      if (typeof token !== 'string' || !token.trim() || !isValidUserInfo(userInfo)) {
        throw new Error('登录响应缺少 Token 或用户信息')
      }

      this.token = token
      this.userInfo = userInfo

      const tokenStored = safeSetItem(TOKEN_KEY, token)
      const userInfoStored = safeSetItem(USER_INFO_KEY, JSON.stringify(userInfo))
      if (!tokenStored || !userInfoStored) {
        clearStoredSession()
      }
    },

    markPasswordChanged() {
      if (!this.userInfo) return
      this.userInfo = { ...this.userInfo, needChangePassword: false }
      safeSetItem(USER_INFO_KEY, JSON.stringify(this.userInfo))
    },

    logout() {
      this.token = ''
      this.userInfo = null
      clearStoredSession()
    }
  }
})
