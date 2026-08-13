import request from '@/utils/request'

export const studentApi = {
  getWaterOrders: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/water-orders`, { params }),
  createWaterOrder: (data) => request.post('/water-orders', data),
  getPackages: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/packages`, { params }),
  pickupPackage: (packageId) => request.post(`/packages/${packageId}/pickup`),
  getCreditAppeals: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/credit-appeals`, { params })
}
