import request from '@/utils/request'

export const counselorApi = {
  getLeaveApplications: (params) => request.get('/leave-applications', { params }),
  getLeaveStats: () => request.get('/leave-applications/stats'),
  approveLeave: (applyId) => request.put(`/leave-applications/${applyId}/approve`),
  rejectLeave: (applyId, reason) => request.put(`/leave-applications/${applyId}/reject`, { reason })
}
