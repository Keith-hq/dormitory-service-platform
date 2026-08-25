import request from '@/utils/request'

export const governanceApi = {
  getAdmins: () => request.get('/admins', { params: { _ts: Date.now() } }),
  getStudents: () => request.get('/students', { params: { _ts: Date.now() } }),
  createAdmin: (data) => request.post('/auth/accounts/admins', data),
  updateAdmin: (id, data) => request.put(`/admins/${id}`, data),
  disableAdmin: (id, reason) => request.put(`/admins/${id}/disable`, { reason }),
  enableAdmin: (id) => request.put(`/admins/${id}/enable`),
  resetAdminPassword: (id) => request.post(`/admins/${id}/password`),
  createStudent: async ({ profile, account }) => {
    await request.post('/students', profile)
    try {
      return await request.post('/auth/accounts/students', account)
    } catch (error) {
      // 档案和账号由两个既有契约分别创建；账号失败时补偿删除本次新建档案。
      try {
        await request.delete(`/students/${profile.studentId}`)
      } catch {
        // 保留原始账号创建错误，后台审计可用于定位补偿失败。
      }
      throw error
    }
  },
  updateStudent: (id, data) => request.put(`/students/${id}`, data),
  disableStudent: (id, reason) => request.put(`/students/${id}/disable`, { reason }),
  resetStudentPassword: (id) => request.post(`/students/${id}/password`),
  getAuditEvents: (params) => request.get('/audit-events', { params }),
  getReport: (type, params) => request.get(`/reports/${type}`, { params })
}
