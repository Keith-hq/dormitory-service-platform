import request from '@/utils/request'

export const adminApi = {
  getAccessDensity: (params) => request.get('/access-logs/density', { params }),
  createViolation: (data) => request.post('/violations', data),
  getViolations: (params) => request.get('/violations', { params }),
  registerVisitor: (data) => request.post('/visitor-registry', data),
  verifyVisitor: (registryId, data) => request.post(`/visitor-registry/${registryId}/verify`, data),
  recordVisitorExit: (registryId) => request.post(`/visitor-registry/${registryId}/exit`),
  listVisitors: () => request.get('/visitor-registry'),
  getNotices: (params) => request.get('/notices', { params }),
  createNotice: (data) => request.post('/notices', data),
  getHygieneRankings: (params) => request.get('/hygiene-rankings', { params }),
  getRoomHygieneRecords: (roomId) => request.get(`/rooms/${roomId}/hygiene`),
  createHygieneRecord: (data) => request.post('/hygiene-records', data),
  updateHygieneRecord: (recordId, data) => request.put(`/hygiene-records/${recordId}`, data),
  createLateEntry: (data) => request.post('/late-entries', data),
  getCreditAppeals: (params) => request.get('/credit-appeals', { params }),
  reviewCreditAppeal: (appealId, data) => request.put(`/credit-appeals/${appealId}/review`, data),
  getRepairTickets: (adminId, params) =>
    request.get(`/admins/${adminId}/repair-tickets`, { params }),
  claimRepairTicket: (ticketId) => request.post(`/repair-tickets/${ticketId}/claim`),
  completeRepairTicket: (ticketId, data) => request.post(`/repair-tickets/${ticketId}/logs`, data)
}
