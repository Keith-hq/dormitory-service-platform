const MOCK_ACCOUNT = {
  loginName: 'student001',
  password: '123456',
  userInfo: {
    id: 'student001',
    name: '测试学生',
    role: 'student'
  }
}

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
  const isValid =
    credentials.loginName === MOCK_ACCOUNT.loginName &&
    credentials.password === MOCK_ACCOUNT.password

  if (!isValid) {
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
        token: 'mock-token-student001',
        userInfo: { ...MOCK_ACCOUNT.userInfo }
      }
    },
    headers: {},
    status: 200,
    statusText: 'OK'
  }
}

export const resolveAuthMockAdapter = (config) => (isLoginRequest(config) ? mockLoginAdapter : null)
