import request from '@/utils/request'

export const accommodationApi = {
  createAllocation: (data) => request.post('/allocations', data),
  transferAllocation: (allocationId, data) =>
    request.post(`/allocations/${allocationId}/transfer`, data),
  getRoomOccupants: (roomId) => request.get(`/rooms/${roomId}/occupants`),
  registerCheckout: (allocationId, data) =>
    request.post(`/allocations/${allocationId}/checkout-register`, data),
  getCheckout: (checkoutId) => request.get(`/checkouts/${checkoutId}`),
  settleCheckout: (checkoutId) => request.post(`/checkouts/${checkoutId}/settle`),
  confirmCheckout: (checkoutId, data) => request.post(`/checkouts/${checkoutId}/confirm`, data),
  cancelCheckout: (checkoutId) => request.post(`/checkouts/${checkoutId}/cancel`)
}
