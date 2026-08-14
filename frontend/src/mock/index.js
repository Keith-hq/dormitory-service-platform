import { resolveAuthMockAdapter } from '@/mock/auth'
import { resolveAnnualReportMockAdapter } from '@/mock/annualReport'
import { resolveBuildingMockAdapter } from '@/mock/building'

const mockResolvers = [
  resolveAuthMockAdapter,
  resolveBuildingMockAdapter,
  resolveAnnualReportMockAdapter
]

export const attachMockAdapter = (config) => {
  if (!import.meta.env.DEV || import.meta.env.VITE_USE_MOCK !== 'true') return config

  const adapter = mockResolvers.map((resolveAdapter) => resolveAdapter(config)).find(Boolean)
  if (adapter) config.adapter = adapter

  return config
}
