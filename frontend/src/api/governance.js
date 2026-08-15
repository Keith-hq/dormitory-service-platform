import request from '@/utils/request'

export const governanceApi = {
  getAdmins: () => request.get('/admins'),
  getStudents: () => request.get('/students'),
  getAuditEvents: (params) => request.get('/audit-events', { params }),
  getReport: (type, params) => request.get(`/reports/${type}`, { params })
}
