export const ANNUAL_REPORT_REFERENCES = Object.freeze({
  utilityTotal: 1200,
  hygieneAvg: 100,
  accessCount: 1200
})

const toNonNegativeNumber = (value) => {
  const normalized = Number(value)
  return Number.isFinite(normalized) && normalized >= 0 ? normalized : 0
}

export const normalizeAnnualReport = (payload, fallbackYear) => {
  const year = Number(payload?.year)

  return {
    year: Number.isInteger(year) ? year : fallbackYear,
    utilityTotal: toNonNegativeNumber(payload?.utilityTotal),
    hygieneAvg: toNonNegativeNumber(payload?.hygieneAvg),
    accessCount: Math.round(toNonNegativeNumber(payload?.accessCount)),
    overview: typeof payload?.overview === 'string' ? payload.overview.trim() : ''
  }
}

export const getMetricProgress = (value, reference) => {
  const normalizedValue = toNonNegativeNumber(value)
  const normalizedReference = toNonNegativeNumber(reference)
  if (!normalizedReference) return 0
  return Math.min(100, Math.round((normalizedValue / normalizedReference) * 1000) / 10)
}

export const getDaysRepresented = (year, now = new Date()) => {
  if (!Number.isInteger(year)) return 365
  if (year !== now.getFullYear()) {
    const start = Date.UTC(year, 0, 1)
    const end = Date.UTC(year + 1, 0, 1)
    return Math.round((end - start) / 86400000)
  }

  const start = Date.UTC(year, 0, 1)
  const today = Date.UTC(year, now.getMonth(), now.getDate())
  return Math.floor((today - start) / 86400000) + 1
}
