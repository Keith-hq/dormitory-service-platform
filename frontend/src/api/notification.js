import request from '@/utils/request'

/**
 * 通知中心 API。
 */
export const notificationApi = {
  /** 分页查询当前用户的通知。 */
  getList: (params) => request.get('/notifications', { params }),

  /** 标记单条通知为已读。 */
  markRead: (notificationId) => request.put(`/notifications/${notificationId}/read`),

  /** 批量标记通知为已读。 */
  markBatchRead: (notificationIds) =>
    request.post('/notifications/read-batch', { notificationIds }),

  /** 查询当前用户未读通知数量。 */
  getUnreadCount: () => request.get('/notifications/unread-count')
}
