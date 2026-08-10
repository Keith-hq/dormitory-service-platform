const MOCK_DELAY = 240
const BUILDING_PATH_PATTERN = /^\/buildings(?:\/(\d+))?\/?$/

const initialBuildings = [
  { buildingId: 1, buildingName: '1号楼', buildingType: '男生宿舍', floorCount: 6 },
  { buildingId: 2, buildingName: '2号楼', buildingType: '女生宿舍', floorCount: 6 },
  { buildingId: 3, buildingName: '3号楼', buildingType: '混合宿舍', floorCount: 8 },
  { buildingId: 4, buildingName: '桂苑 A 栋', buildingType: '男生宿舍', floorCount: 7 },
  { buildingId: 5, buildingName: '桂苑 B 栋', buildingType: '女生宿舍', floorCount: 7 },
  { buildingId: 6, buildingName: '松苑 A 栋', buildingType: '男生宿舍', floorCount: 5 },
  { buildingId: 7, buildingName: '松苑 B 栋', buildingType: '混合宿舍', floorCount: 5 },
  { buildingId: 8, buildingName: '梅苑 A 栋', buildingType: '女生宿舍', floorCount: 9 },
  { buildingId: 9, buildingName: '梅苑 B 栋', buildingType: '男生宿舍', floorCount: 9 },
  { buildingId: 10, buildingName: '竹苑 A 栋', buildingType: '混合宿舍', floorCount: 6 },
  { buildingId: 11, buildingName: '竹苑 B 栋', buildingType: '女生宿舍', floorCount: 6 },
  { buildingId: 12, buildingName: '留学生公寓', buildingType: '混合宿舍', floorCount: 12 }
].map((building, index) => ({
  ...building,
  createTime: `2026-08-${String(index + 1).padStart(2, '0')}T08:00:00`
}))

let buildings = initialBuildings.map((building) => ({ ...building }))

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
  return pathname.replace(/^\/api(?=\/)/, '')
}

const createResponse = (config, { code = 200, message = '操作成功', data = {}, status = 200 }) => ({
  config,
  data: { code, message, data },
  headers: { 'content-type': 'application/json' },
  status,
  statusText: status >= 400 ? 'Error' : 'OK'
})

const normalizePositiveInteger = (value, fallback) => {
  const normalized = Number(value)
  return Number.isInteger(normalized) && normalized > 0 ? normalized : fallback
}

const validateBuilding = (payload, { partial = false } = {}) => {
  if (!partial || Object.hasOwn(payload, 'buildingName')) {
    if (typeof payload.buildingName !== 'string' || !payload.buildingName.trim()) {
      return '楼栋名称不能为空'
    }
  }

  if (!partial || Object.hasOwn(payload, 'buildingType')) {
    if (typeof payload.buildingType !== 'string' || !payload.buildingType.trim()) {
      return '楼栋类型不能为空'
    }
  }

  if (!partial || Object.hasOwn(payload, 'floorCount')) {
    const floorCount = Number(payload.floorCount)
    if (!Number.isInteger(floorCount) || floorCount < 1 || floorCount > 50) {
      return '楼层数必须在 1~50 之间'
    }
  }

  return ''
}

const getPagedBuildings = (config) => {
  const page = normalizePositiveInteger(config.params?.page, 1)
  const pageSize = normalizePositiveInteger(config.params?.pageSize, 10)
  const buildingType = config.params?.buildingType
  const filtered = buildingType
    ? buildings.filter((building) => building.buildingType === buildingType)
    : buildings
  const start = (page - 1) * pageSize

  return createResponse(config, {
    data: {
      items: filtered.slice(start, start + pageSize).map((building) => ({ ...building })),
      total: filtered.length,
      page,
      pageSize
    }
  })
}

const getBuilding = (config, buildingId) => {
  const building = buildings.find((item) => item.buildingId === buildingId)
  return building
    ? createResponse(config, { data: { ...building } })
    : createResponse(config, { code: 404, message: '楼栋不存在', data: null, status: 404 })
}

const createBuilding = (config) => {
  const payload = parseRequestData(config.data)
  const validationMessage = validateBuilding(payload)
  if (validationMessage) {
    return createResponse(config, {
      code: 400,
      message: validationMessage,
      data: null,
      status: 400
    })
  }

  const building = {
    buildingId: Math.max(0, ...buildings.map((item) => item.buildingId)) + 1,
    buildingName: payload.buildingName.trim(),
    buildingType: payload.buildingType.trim(),
    floorCount: Number(payload.floorCount),
    createTime: new Date().toISOString()
  }
  buildings = [building, ...buildings]

  return createResponse(config, {
    message: '新增成功',
    data: { ...building }
  })
}

const updateBuilding = (config, buildingId) => {
  const index = buildings.findIndex((item) => item.buildingId === buildingId)
  if (index === -1) {
    return createResponse(config, { code: 404, message: '楼栋不存在', data: null, status: 404 })
  }

  const payload = parseRequestData(config.data)
  const validationMessage = validateBuilding(payload, { partial: true })
  if (validationMessage) {
    return createResponse(config, {
      code: 400,
      message: validationMessage,
      data: null,
      status: 400
    })
  }

  const current = buildings[index]
  const updated = {
    ...current,
    ...(Object.hasOwn(payload, 'buildingName') && { buildingName: payload.buildingName.trim() }),
    ...(Object.hasOwn(payload, 'buildingType') && { buildingType: payload.buildingType.trim() }),
    ...(Object.hasOwn(payload, 'floorCount') && { floorCount: Number(payload.floorCount) })
  }
  buildings = buildings.map((building) => (building.buildingId === buildingId ? updated : building))

  return createResponse(config, { message: '更新成功', data: { ...updated } })
}

const deleteBuilding = (config, buildingId) => {
  const exists = buildings.some((building) => building.buildingId === buildingId)
  if (!exists) {
    return createResponse(config, { code: 404, message: '楼栋不存在', data: null, status: 404 })
  }

  buildings = buildings.filter((building) => building.buildingId !== buildingId)
  return createResponse(config, { message: '删除成功' })
}

const buildingMockAdapter = async (config) => {
  await wait(MOCK_DELAY)
  const match = getRequestPath(config).match(BUILDING_PATH_PATTERN)
  const buildingId = match?.[1] ? Number(match[1]) : null
  const method = config.method?.toLowerCase()

  if (method === 'get' && buildingId === null) return getPagedBuildings(config)
  if (method === 'get') return getBuilding(config, buildingId)
  if (method === 'post' && buildingId === null) return createBuilding(config)
  if (method === 'put' && buildingId !== null) return updateBuilding(config, buildingId)
  if (method === 'delete' && buildingId !== null) return deleteBuilding(config, buildingId)

  return createResponse(config, {
    code: 405,
    message: 'Mock 接口不支持该请求方法',
    data: null,
    status: 405
  })
}

export const resolveBuildingMockAdapter = (config) =>
  BUILDING_PATH_PATTERN.test(getRequestPath(config)) ? buildingMockAdapter : null
