import request from '@/utils/request'

export const utilityApi = {
  getBills: (params) => request.get('/utility-fees', { params }),
  createBill: (data) => request.post('/utility-fees', data),
  updateBill: (feeId, data) => request.put(`/utility-fees/${feeId}`, data),
  publishBill: (feeId) => request.post(`/utility-fees/${feeId}/publish`),
  allocateBill: (feeId) => request.post(`/utility-fees/${feeId}/allocate`),
  getBillDetails: (feeId) => request.get(`/utility-fees/${feeId}/details`),
  getPowerStatus: (roomId) => request.get(`/rooms/${roomId}/power-status`)
}
