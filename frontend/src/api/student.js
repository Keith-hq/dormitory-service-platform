import request from '@/utils/request'
import { normalizePackagePayload } from '@/utils/package'

export const studentApi = {
  getWaterOrders: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/water-orders`, { params }),
  createWaterOrder: (data) => request.post('/water-orders', data),
  getPackages: async (studentId, params) => {
    const payload = await request.get(`/students/${encodeURIComponent(studentId)}/packages`, {
      params
    })
    return normalizePackagePayload(payload)
  },
  pickupPackage: (packageId) => request.post(`/packages/${packageId}/pickup`),
  getCreditAppeals: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/credit-appeals`, { params })
}
