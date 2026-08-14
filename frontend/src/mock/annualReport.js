const MOCK_DELAY = 260
const ANNUAL_REPORT_PATTERN = /^\/students\/[^/]+\/reports\/annual\/?$/

const reports = {
  2024: {
    year: 2024,
    utilityTotal: 684.5,
    hygieneAvg: 91.8,
    accessCount: 842,
    overview: '这一年，你的宿舍生活安稳而有节奏。卫生表现持续优秀，水电支出保持平稳。'
  },
  2025: {
    year: 2025,
    utilityTotal: 756.8,
    hygieneAvg: 94.2,
    accessCount: 1016,
    overview: '你在忙碌与规律之间找到了平衡。宿舍卫生稳步提升，校园生活也更加活跃。'
  },
  2026: {
    year: 2026,
    utilityTotal: 512.4,
    hygieneAvg: 95.6,
    accessCount: 719,
    overview:
      '截至目前，你保持了清爽、规律的居住状态。每一次归寝与共同维护，都构成这一年的生活轨迹。'
  }
}

const wait = (duration) => new Promise((resolve) => globalThis.setTimeout(resolve, duration))

const getRequestPath = (config) => {
  const pathname = new URL(config.url || '/', 'https://mock.local').pathname
  return pathname.replace(/^\/api(?=\/)/, '')
}

const createResponse = (config, { code = 200, message = '查询成功', data, status = 200 }) => ({
  config,
  data: { code, message, data },
  headers: { 'content-type': 'application/json' },
  status,
  statusText: status >= 400 ? 'Error' : 'OK'
})

const annualReportMockAdapter = async (config) => {
  await wait(MOCK_DELAY)
  const year = Number(config.params?.year || new Date().getFullYear())
  const report = reports[year]

  if (!report) {
    return createResponse(config, {
      code: 404,
      message: '该年度暂无生活报告',
      data: null,
      status: 404
    })
  }

  return createResponse(config, { data: { ...report } })
}

export const resolveAnnualReportMockAdapter = (config) => {
  const isRequest =
    config.method?.toLowerCase() === 'get' && ANNUAL_REPORT_PATTERN.test(getRequestPath(config))
  return isRequest ? annualReportMockAdapter : null
}
