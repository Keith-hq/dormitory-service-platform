import request from '@/utils/request'

export const reportApi = {
  getAnnual: (studentId, year) =>
    request.get(`/students/${encodeURIComponent(studentId)}/reports/annual`, {
      params: { year }
    })
}
