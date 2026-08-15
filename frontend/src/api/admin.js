import request from '@/utils/request'

export const adminApi = {
  getAccessLogs: (params) => request.get('/access-logs', { params }),
  getAccessDensity: (params) => request.get('/access-logs/density', { params }),
  getViolations: (params) => request.get('/violations', { params }),
  createViolation: (data) => request.post('/violations', data),
  registerVisitor: (data) => request.post('/visitor-registry', data),
  verifyVisitor: (registryId, data) => request.post(`/visitor-registry/${registryId}/verify`, data),
  recordVisitorExit: (registryId) => request.post(`/visitor-registry/${registryId}/exit`),
  getNotices: (params) => request.get('/notices', { params }),
  createNotice: (data) => request.post('/notices', data),
  getHygieneRankings: (params) => request.get('/hygiene-rankings', { params }),
  createHygieneRecord: (data) => request.post('/hygiene-records', data),
  getRepairTickets: (adminId, params) =>
    request.get(`/admins/${adminId}/repair-tickets`, { params }),
  claimRepairTicket: (ticketId) => request.post(`/repair-tickets/${ticketId}/claim`),
  completeRepairTicket: (ticketId, data) => request.post(`/repair-tickets/${ticketId}/logs`, data)
}
