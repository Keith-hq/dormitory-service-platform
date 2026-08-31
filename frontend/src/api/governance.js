import request from '@/utils/request'

export const governanceApi = {
  getAdmins: () => request.get('/admins', { params: { _ts: Date.now() } }),
  deleteViolation: (id, reason) => request.delete(`/violations/${id}`, { data: { reason } }),
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
  enableStudent: (id) => request.put(`/students/${id}/enable`),
  resetStudentPassword: (id) => request.post(`/students/${id}/password`),
  getAuditEvents: (params) => request.get('/audit-events', { params }),
  getReport: (type, params) => request.get(`/reports/${type}`, { params }),
  // C11：Excel 批量导入学生（IMPORT-01，multipart file）
  importStudents: (formData) =>
    request.post('/students/import', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    }),
  // C11：学院管理（SUPER-01）
  getColleges: () => request.get('/colleges'),
  createCollege: (data) => request.post('/colleges', data),
  deleteCollege: (id) => request.delete(`/colleges/${id}`),
  // C11：专业管理（SUPER-02）
  getMajors: () => request.get('/majors'),
  createMajor: (data) => request.post('/majors', data),
  deleteMajor: (id) => request.delete(`/majors/${id}`)
}
