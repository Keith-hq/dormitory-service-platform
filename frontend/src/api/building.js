import request from '@/utils/request'

/**
 * 楼栋管理 API
 */
export const buildingApi = {
  /** 分页查询楼栋列表 */
  getList: (params) => request.get('/building', { params }),

  /** 根据 ID 查询楼栋详情 */
  getById: (id) => request.get(`/building/${id}`),

  /** 新增楼栋 */
  create: (data) => request.post('/building', data),

  /** 编辑楼栋 */
  update: (id, data) => request.put(`/building/${id}`, data),

  /** 删除楼栋 */
  delete: (id) => request.delete(`/building/${id}`)
}
