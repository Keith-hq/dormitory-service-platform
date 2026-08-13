import request from '@/utils/request'

export const adminApi = {
  getWaterOrders: (params) => request.get('/water-orders', { params }),
  markWaterOrderDelivering: (orderId) => request.post(`/water-orders/${orderId}/deliver`),
  confirmWaterOrderDelivered: (orderId) => request.post(`/water-orders/${orderId}/confirm`),
  getAccessLogs: (params) => request.get('/access-logs', { params }),
  getViolations: (params) => request.get('/violations', { params })
}
