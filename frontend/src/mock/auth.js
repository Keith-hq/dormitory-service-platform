const MOCK_ACCOUNTS = [
  {
    loginName: 'student001',
    password: '123456',
    userInfo: {
      id: 'S2026001',
      name: '林晓满',
      role: 'student',
      buildingName: '桂苑 A 栋',
      roomName: '503'
    }
  },
  {
    loginName: 'admin001',
    password: '123456',
    userInfo: {
      id: 'A2026001',
      name: '周值班',
      role: 'admin',
      buildingName: '桂苑 A 栋'
    }
  },
  {
    loginName: 'repair001',
    password: '123456',
    userInfo: { id: 'A2026002', name: '陈维修', role: 'repairman', buildingName: '桂苑 A 栋' }
  },
  {
    loginName: 'counselor001',
    password: '123456',
    userInfo: { id: 'C2026001', name: '李老师', role: 'counselor' }
  },
  {
    loginName: 'super001',
    password: '123456',
    userInfo: { id: 'A2026000', name: '平台管理员', role: 'super_admin' }
  }
]

const MOCK_LOGIN_PATH = '/auth/login'
const MOCK_DELAY = 350

const wait = (duration) => new Promise((resolve) => globalThis.setTimeout(resolve, duration))

const parseRequestData = (data) => {
  if (!data) return {}
  if (typeof data === 'object') return data

  try {
    return JSON.parse(data)
  } catch {
    return {}
  }
}

const isLoginRequest = (config) => {
  const path = config.url?.split('?')[0].replace(/\/$/, '')
  return config.method?.toLowerCase() === 'post' && path === MOCK_LOGIN_PATH
}

const mockLoginAdapter = async (config) => {
  await wait(MOCK_DELAY)
  const credentials = parseRequestData(config.data)
  const account = MOCK_ACCOUNTS.find(
    (item) => credentials.loginName === item.loginName && credentials.password === item.password
  )

  if (!account) {
    return {
      config,
      data: {
        code: 401,
        message: '登录名或密码错误',
        data: null
      },
      headers: {},
      status: 401,
      statusText: 'Unauthorized'
    }
  }

  return {
    config,
    data: {
      code: 200,
      message: '登录成功',
      data: {
        token: `mock-token-${account.userInfo.role}-${account.userInfo.id}`,
        userInfo: { ...account.userInfo }
      }
    },
    headers: {},
    status: 200,
    statusText: 'OK'
  }
}

export const resolveAuthMockAdapter = (config) => (isLoginRequest(config) ? mockLoginAdapter : null)
