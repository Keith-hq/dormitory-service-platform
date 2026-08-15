export const PACKAGE_STATUS = Object.freeze({
  READY: '待取件',
  PICKED_UP: '已取件'
})

export const isPackageReady = (status) => status === PACKAGE_STATUS.READY

export const normalizePackage = (item) => {
  if (!item || typeof item !== 'object') return item

  const packageId = item.packageId ?? item.parcelId
  const fallbackCode = packageId === null || packageId === undefined ? '' : String(packageId)

  return {
    ...item,
    packageId,
    expressNo: item.expressNo || fallbackCode,
    carrier: item.carrier || item.courierCompany || '',
    pickupCode: item.pickupCode || fallbackCode,
    arrivalTime: item.arrivalTime || item.arriveTime,
    status: item.status || (item.pickupTime ? PACKAGE_STATUS.PICKED_UP : PACKAGE_STATUS.READY)
  }
}

export const normalizePackagePayload = (payload) => {
  if (Array.isArray(payload)) return payload.map(normalizePackage)
  if (!payload || typeof payload !== 'object') return payload

  for (const key of ['items', 'records', 'list']) {
    if (Array.isArray(payload[key])) {
      return { ...payload, [key]: payload[key].map(normalizePackage) }
    }
  }

  return payload
}

export const formatPackageArrivalTime = (value) => {
  if (!value) return '—'

  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return value

  return new Intl.DateTimeFormat('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  }).format(date)
}
