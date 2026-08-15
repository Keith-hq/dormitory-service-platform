const MOCK_DELAY = 220

let waterOrders = [
  {
    orderId: 3108,
    studentId: 'S2026001',
    studentName: '林晓满',
    roomName: '桂苑 A 栋 · 503',
    quantity: 2,
    amount: 16,
    status: 'pending',
    createdAt: '2026-08-12 09:20'
  },
  {
    orderId: 3107,
    studentId: 'S2026001',
    studentName: '林晓满',
    roomName: '桂苑 A 栋 · 503',
    quantity: 1,
    amount: 8,
    status: 'delivering',
    createdAt: '2026-08-11 17:45'
  },
  {
    orderId: 3106,
    studentId: 'S2026008',
    studentName: '陈宁',
    roomName: '桂苑 A 栋 · 407',
    quantity: 3,
    amount: 24,
    status: 'pending',
    createdAt: '2026-08-11 15:10'
  },
  {
    orderId: 3099,
    studentId: 'S2026001',
    studentName: '林晓满',
    roomName: '桂苑 A 栋 · 503',
    quantity: 1,
    amount: 8,
    status: 'completed',
    createdAt: '2026-08-08 12:12'
  }
]

let packages = [
  {
    packageId: 7102,
    studentId: 'S2026001',
    expressNo: 'SF142***290',
    carrier: '顺丰速运',
    pickupCode: '5-1832',
    arrivalTime: '2026-08-13T10:36:00+08:00',
    status: '待取件'
  },
  {
    packageId: 7094,
    studentId: 'S2026001',
    expressNo: 'ZT778***051',
    carrier: '中通快递',
    pickupCode: '2-6401',
    arrivalTime: '2026-08-12T16:20:00+08:00',
    status: '待取件'
  },
  {
    packageId: 7031,
    studentId: 'S2026001',
    expressNo: 'JD009***714',
    carrier: '京东物流',
    pickupCode: 'C-0205',
    arrivalTime: '2026-08-09T11:08:00+08:00',
    status: '已取件'
  }
]

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

const parseRequestData = (data) => {
  if (!data) return {}
  if (typeof data === 'object') return data
  try {
    return JSON.parse(data)
  } catch {
    return {}
  }
}

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

const updateWaterOrder = (config, orderId, { expectedStatus, nextStatus, message }) => {
  const index = waterOrders.findIndex((item) => item.orderId === orderId)
  if (index === -1) {
    return createResponse(config, { code: 404, message: '订水订单不存在', data: null, status: 404 })
  }
  const current = waterOrders[index]
  if (current.status === nextStatus) {
    return createResponse(config, { message, data: { ...current } })
  }
  if (current.status !== expectedStatus) {
    return createResponse(config, {
      code: 409,
      message: '订单当前状态不允许执行此操作',
      data: null,
      status: 409
    })
  }
  waterOrders = waterOrders.map((item, itemIndex) =>
    itemIndex === index ? { ...item, status: nextStatus } : item
  )
  return createResponse(config, { message, data: { ...waterOrders[index] } })
}

const roleServiceMockAdapter = async (config) => {
  await wait(MOCK_DELAY)
  const path = getRequestPath(config)
  const method = config.method?.toLowerCase()
  const role = getMockRole(config)

  if (!role) {
    return createResponse(config, { code: 401, message: '登录状态已失效', data: null, status: 401 })
  }

  let match = path.match(/^\/students\/([^/]+)\/water-orders$/)
  if (match && method === 'get') {
    if (role !== 'student') return forbidden(config)
    return createResponse(config, {
      data: asCollection(
        waterOrders.filter((item) => item.studentId === decodeURIComponent(match[1]))
      )
    })
  }

  match = path.match(/^\/students\/([^/]+)\/packages$/)
  if (match && method === 'get') {
    if (role !== 'student') return forbidden(config)
    return createResponse(config, {
      data: asCollection(packages.filter((item) => item.studentId === decodeURIComponent(match[1])))
    })
  }

  match = path.match(/^\/students\/([^/]+)\/credit-appeals$/)
  if (match && method === 'get') {
    if (role !== 'student') return forbidden(config)
    return createResponse(config, {
      data: asCollection(
        creditAppeals.filter((item) => item.studentId === decodeURIComponent(match[1]))
      )
    })
  }

  if (path === '/water-orders' && method === 'post') {
    if (role !== 'student') return forbidden(config)
    const payload = parseRequestData(config.data)
    const quantity = Number(payload.quantity)
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > 4) {
      return createResponse(config, {
        code: 400,
        message: '订水数量须为 1 至 4 桶',
        data: null,
        status: 400
      })
    }
    const order = {
      orderId: Math.max(...waterOrders.map((item) => item.orderId)) + 1,
      studentId: 'S2026001',
      studentName: '林晓满',
      roomName: '桂苑 A 栋 · 503',
      quantity,
      amount: quantity * 8,
      status: 'pending',
      createdAt: '刚刚'
    }
    waterOrders = [order, ...waterOrders]
    return createResponse(config, {
      code: 201,
      message: '订水成功',
      data: { ...order },
      status: 201
    })
  }

  match = path.match(/^\/packages\/(\d+)\/pickup$/)
  if (match && method === 'post') {
    if (role !== 'student') return forbidden(config)
    const packageId = Number(match[1])
    const target = packages.find((item) => item.packageId === packageId)
    if (!target) {
      return createResponse(config, { code: 404, message: '快递不存在', data: null, status: 404 })
    }
    packages = packages.map((item) =>
      item.packageId === packageId ? { ...item, status: '已取件' } : item
    )
    return createResponse(config, { message: '取件成功', data: null })
  }

  if (path === '/water-orders' && method === 'get') {
    if (role !== 'admin') return forbidden(config)
    return createResponse(config, { data: asCollection(waterOrders) })
  }

  match = path.match(/^\/water-orders\/(\d+)\/(deliver|confirm)$/)
  if (match && method === 'post') {
    if (role !== 'admin') return forbidden(config)
    const isDeliver = match[2] === 'deliver'
    return updateWaterOrder(
      config,
      Number(match[1]),
      isDeliver
        ? {
            expectedStatus: 'pending',
            nextStatus: 'delivering',
            message: '已标记配送'
          }
        : {
            expectedStatus: 'delivering',
            nextStatus: 'completed',
            message: '已确认送达'
          }
    )
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
  /^\/students\/[^/]+\/(?:water-orders|packages|credit-appeals)\/?$/,
  /^\/water-orders(?:\/\d+\/(?:deliver|confirm))?\/?$/,
  /^\/packages\/\d+\/pickup\/?$/,
  /^\/access-logs\/?$/,
  /^\/violations\/?$/
]

export const resolveRoleServiceMockAdapter = (config) => {
  const path = getRequestPath(config)
  return ROLE_SERVICE_PATTERNS.some((pattern) => pattern.test(path)) ? roleServiceMockAdapter : null
}
