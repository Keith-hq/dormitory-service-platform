import request from '@/utils/request'

/**
 * 楼栋管理 API
 */
export const buildingApi = {
  /** 分页查询楼栋列表 */
  getList: (params) => request.get('/buildings', { params }),

  /** 根据 ID 查询楼栋详情 */
  getById: (id) => request.get(`/buildings/${id}`),

  /** 新增楼栋 */
  create: (data) => request.post('/buildings', data),

  /** 编辑楼栋 */
  update: (id, data) => request.put(`/buildings/${id}`, data),

  /** 删除楼栋 */
  delete: (id) => request.delete(`/buildings/${id}`)
}
