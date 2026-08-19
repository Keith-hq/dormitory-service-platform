import request from '@/utils/request'

export const assetApi = {
  getRoomAssets: (roomId) => request.get(`/rooms/${roomId}/assets`),
  createAsset: (data) => request.post('/assets', data),
  updateAsset: (assetId, data) => request.put(`/assets/${assetId}`, data),
  deleteAsset: (assetId) => request.delete(`/assets/${assetId}`),
  stocktakeAsset: (assetId, data) => request.post(`/assets/${assetId}/stocktake`, data),
  sendAssetToRepair: (assetId, data) => request.post(`/assets/${assetId}/to-repair`, data),
  getWarnings: (params) => request.get('/assets/warnings', { params }),
  handleWarning: (assetId, data) => request.put(`/assets/warnings/${assetId}/handle`, data),
  getCleaningTasks: (params) => request.get('/cleaning-tasks', { params }),
  completeCleaningTask: (taskId) => request.put(`/cleaning-tasks/${taskId}/complete`),
  getSharedItems: (params) => request.get('/shared-items', { params }),
  createSharedItem: (data) => request.post('/shared-items', data),
  updateSharedItem: (itemId, data) => request.put(`/shared-items/${itemId}`, data),
  deleteSharedItem: (itemId) => request.delete(`/shared-items/${itemId}`)
}
