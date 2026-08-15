export const PACKAGE_STATUS = Object.freeze({
  READY: '待取件',
  PICKED_UP: '已取件'
})

export const isPackageReady = (status) => status === PACKAGE_STATUS.READY

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
