export const normalizeCollection = (payload) => {
  if (Array.isArray(payload)) {
    return { items: payload, total: payload.length }
  }

  if (!payload || typeof payload !== 'object') {
    return { items: [], total: 0 }
  }

  const items = [payload.items, payload.records, payload.list].find(Array.isArray) || []
  const total = Number(payload.total ?? payload.totalCount ?? items.length)

  return {
    items,
    total: Number.isFinite(total) && total >= 0 ? total : items.length
  }
}
