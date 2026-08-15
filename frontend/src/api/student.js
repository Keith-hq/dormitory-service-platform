import request from '@/utils/request'

export const studentApi = {
  getAccommodation: (studentId) =>
    request.get(`/students/${encodeURIComponent(studentId)}/accommodation`),
  getAccommodationHistory: (studentId) =>
    request.get(`/students/${encodeURIComponent(studentId)}/accommodation/history`),
  updateProfile: (studentId, data) =>
    request.put(`/students/${encodeURIComponent(studentId)}/profile`, data),
  getWallet: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/wallet`, { params }),
  getFees: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/fees`, { params }),
  payFee: (detailId, idempotencyKey) =>
    request.post(
      '/wallet/payments',
      { detailId },
      { headers: { 'Idempotency-Key': idempotencyKey } }
    ),
  rechargeWallet: (amount, idempotencyKey) =>
    request.post(
      '/wallet/recharges',
      { amount },
      { headers: { 'Idempotency-Key': idempotencyKey } }
    ),
  getRepairTickets: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/repair-tickets`, { params }),
  createRepairTicket: (data) => request.post('/repair-tickets', data),
  cancelRepairTicket: (ticketId) => request.post(`/repair-tickets/${ticketId}/cancel`),
  getFacilities: (params) => request.get('/facilities', { params }),
  createFacilityBooking: (data) => request.post('/facility-bookings', data),
  getSharedItems: (params) => request.get('/shared-items', { params }),
  getItemLoans: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/item-loans`, { params }),
  createItemLoan: (itemId, idempotencyKey) =>
    request.post('/item-loans', { itemId }, { headers: { 'Idempotency-Key': idempotencyKey } }),
  returnItemLoan: (loanId, idempotencyKey) =>
    request.post(
      `/item-loans/${loanId}/return`,
      {},
      { headers: { 'Idempotency-Key': idempotencyKey } }
    ),
  getLateEntries: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/late-entries`, { params }),
  updateLateEntryReason: (recordId, reason) =>
    request.put(`/late-entries/${recordId}/reason`, { reason }),
  getLeaveApplications: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/leave-applications`, { params }),
  createLeaveApplication: (data) => request.post('/leave-applications', data),
  getRoomVotes: (roomId, params) => request.get(`/rooms/${roomId}/votes`, { params }),
  createRoomVote: (data) => request.post('/room-votes', data),
  submitVoteResponse: (voteId, data) => request.post(`/room-votes/${voteId}/responses`, data),
  getVisitorAuthorizations: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/visitor-authorizations`, { params }),
  createVisitorAuthorization: (data) => request.post('/visitor-authorizations', data),
  getCredit: (studentId) => request.get(`/students/${encodeURIComponent(studentId)}/credit`),
  getMonthlyFeeReport: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/reports/monthly-fee`, { params }),
  getFacilityUsageReport: (studentId, params) =>
    request.get(`/students/${encodeURIComponent(studentId)}/reports/facility-usage`, { params })
}
