const MOCK_DELAY = 220

const creditAppeals = [
  {
    appealId: 901,
    studentId: 'S2026001',
    reason: '晚归记录时间与门禁日志不一致',
    scoreChange: -2,
    status: 'reviewing',
    createdAt: '2026-08-10 20:14'
  },
  {
    appealId: 876,
    studentId: 'S2026001',
    reason: '已提交志愿服务补分证明',
    scoreChange: 3,
    status: 'approved',
    createdAt: '2026-07-28 14:40'
  }
]

const accessLogs = [
  {
    logId: 81023,
    studentName: '林晓满',
    roomName: '503',
    direction: '进入',
    accessTime: '2026-08-13 08:42',
    status: 'normal'
  },
  {
    logId: 81022,
    studentName: '陈宁',
    roomName: '407',
    direction: '离开',
    accessTime: '2026-08-13 08:38',
    status: 'normal'
  },
  {
    logId: 81018,
    studentName: '赵可',
    roomName: '601',
    direction: '进入',
    accessTime: '2026-08-13 00:16',
    status: 'late'
  }
]

const violations = [
  {
    violationId: 608,
    studentName: '赵可',
    roomName: '601',
    type: '晚归',
    scoreDelta: -2,
    status: 'pending',
    occurredAt: '2026-08-13 00:16'
  },
  {
    violationId: 602,
    studentName: '王齐',
    roomName: '302',
    type: '公共区域堆物',
    scoreDelta: -1,
    status: 'processed',
    occurredAt: '2026-08-11 18:30'
  }
]

const wait = (duration) => new Promise((resolve) => globalThis.setTimeout(resolve, duration))

const getRequestPath = (config) => {
  const pathname = new URL(config.url || '/', 'https://mock.local').pathname
  return pathname.replace(/^\/api(?=\/)/, '').replace(/\/$/, '') || '/'
}

const getBearerToken = (config) => {
  const headerValue = config.headers?.get?.('Authorization') || config.headers?.Authorization || ''
  return String(headerValue).replace(/^Bearer\s+/i, '')
}

const getMockRole = (config) => {
  const token = getBearerToken(config)
  if (token.startsWith('mock-token-student-')) return 'student'
  if (token.startsWith('mock-token-admin-') || token.startsWith('mock-token-super_admin-')) {
    return 'admin'
  }
  return ''
}

const createResponse = (config, { code = 200, message = '操作成功', data = {}, status = 200 }) => ({
  config,
  data: { code, message, data },
  headers: { 'content-type': 'application/json' },
  status,
  statusText: status >= 400 ? 'Error' : 'OK'
})

const asCollection = (items) => ({ items: items.map((item) => ({ ...item })), total: items.length })

const forbidden = (config) =>
  createResponse(config, { code: 403, message: '当前角色无权访问该接口', data: null, status: 403 })

const roleServiceMockAdapter = async (config) => {
  await wait(MOCK_DELAY)
  const path = getRequestPath(config)
  const method = config.method?.toLowerCase()
  const role = getMockRole(config)

  if (!role) {
    return createResponse(config, { code: 401, message: '登录状态已失效', data: null, status: 401 })
  }

  const match = path.match(/^\/students\/([^/]+)\/credit-appeals$/)
  if (match && method === 'get') {
    if (role !== 'student') return forbidden(config)
    return createResponse(config, {
      data: asCollection(
        creditAppeals.filter((item) => item.studentId === decodeURIComponent(match[1]))
      )
    })
  }

  if (path === '/access-logs' && method === 'get') {
    if (role !== 'admin') return forbidden(config)
    return createResponse(config, { data: asCollection(accessLogs) })
  }

  if (path === '/violations' && method === 'get') {
    if (role !== 'admin') return forbidden(config)
    return createResponse(config, { data: asCollection(violations) })
  }

  return createResponse(config, {
    code: 405,
    message: 'Mock 接口不支持该请求方法',
    data: null,
    status: 405
  })
}

const ROLE_SERVICE_PATTERNS = [
  /^\/students\/[^/]+\/credit-appeals\/?$/,
  /^\/access-logs\/?$/,
  /^\/violations\/?$/
]

export const resolveRoleServiceMockAdapter = (config) => {
  const path = getRequestPath(config)
  return ROLE_SERVICE_PATTERNS.some((pattern) => pattern.test(path)) ? roleServiceMockAdapter : null
}
