import { defineStore } from 'pinia'
import { notificationApi } from '@/api/notification'

const DEFAULT_PAGE = 1
const DEFAULT_PAGE_SIZE = 10
const MAX_PAGE_SIZE = 100
const READ_FILTERS = new Set(['', '已读', '未读'])
let latestFetchId = 0
let latestUnreadCountFetchId = 0
let stateGeneration = 0

const normalizePositiveInteger = (value, fallback) => {
  const normalized = Number(value)
  return Number.isInteger(normalized) && normalized > 0 ? normalized : fallback
}

const normalizePageSize = (value, fallback) =>
  Math.min(normalizePositiveInteger(value, fallback), MAX_PAGE_SIZE)

const normalizeReadFilter = (value) => {
  if (!READ_FILTERS.has(value)) {
    throw new TypeError('isRead 只能为已读、未读或空字符串')
  }
  return value
}

const normalizeNotificationIds = (notificationIds) => {
  if (!Array.isArray(notificationIds)) {
    throw new TypeError('notificationIds 必须为数组')
  }
  if (
    notificationIds.some(
      (notificationId) => !Number.isInteger(notificationId) || notificationId <= 0
    )
  ) {
    throw new TypeError('notificationIds 必须全部为正整数')
  }
  return [...new Set(notificationIds)]
}

const getErrorMessage = (error, fallback) =>
  typeof error?.message === 'string' && error.message.trim() ? error.message : fallback

/**
 * 当前登录用户的通知状态仓库。
 */
export const useNoticeStore = defineStore('notice', {
  state: () => ({
    notices: [],
    total: 0,
    page: DEFAULT_PAGE,
    pageSize: DEFAULT_PAGE_SIZE,
    isRead: '',
    unreadCount: 0,
    loading: false,
    unreadCountLoading: false,
    markingIds: [],
    errorMessage: ''
  }),

  getters: {
    totalPages: (state) => Math.ceil(state.total / state.pageSize),
    hasUnread: (state) => state.unreadCount > 0,
    isMarking: (state) => (notificationId) => state.markingIds.includes(notificationId)
  },

  actions: {
    async fetchNotices(options = {}) {
      const page = normalizePositiveInteger(options.page, this.page)
      const pageSize = normalizePageSize(options.pageSize, this.pageSize)
      const isRead = normalizeReadFilter(options.isRead ?? this.isRead)
      const fetchId = ++latestFetchId
      const generation = stateGeneration

      this.loading = true
      this.errorMessage = ''

      try {
        const result = await notificationApi.getList({
          page,
          pageSize,
          isRead: isRead || undefined
        })

        if (fetchId !== latestFetchId || generation !== stateGeneration) return result

        this.notices = Array.isArray(result?.items) ? result.items : []
        this.total = Math.max(0, Number(result?.total) || 0)
        this.page = normalizePositiveInteger(result?.page, page)
        this.pageSize = normalizePositiveInteger(result?.pageSize, pageSize)
        this.isRead = isRead

        return result
      } catch (error) {
        if (fetchId === latestFetchId && generation === stateGeneration) {
          this.errorMessage = getErrorMessage(error, '获取通知失败')
        }
        throw error
      } finally {
        if (fetchId === latestFetchId && generation === stateGeneration) this.loading = false
      }
    },

    async fetchUnreadCount() {
      const fetchId = ++latestUnreadCountFetchId
      const generation = stateGeneration
      this.unreadCountLoading = true
      this.errorMessage = ''

      try {
        const result = await notificationApi.getUnreadCount()
        if (fetchId !== latestUnreadCountFetchId || generation !== stateGeneration) {
          return this.unreadCount
        }
        this.unreadCount = Math.max(0, Number(result?.count) || 0)
        return this.unreadCount
      } catch (error) {
        if (fetchId === latestUnreadCountFetchId && generation === stateGeneration) {
          this.errorMessage = getErrorMessage(error, '获取未读通知数量失败')
        }
        throw error
      } finally {
        if (fetchId === latestUnreadCountFetchId && generation === stateGeneration) {
          this.unreadCountLoading = false
        }
      }
    },

    async markRead(notificationId) {
      if (!Number.isInteger(notificationId) || notificationId <= 0) {
        throw new TypeError('notificationId 必须为正整数')
      }

      const currentNotice = this.notices.find((notice) => notice.notificationId === notificationId)
      if (currentNotice?.readTime) return
      if (this.markingIds.includes(notificationId)) return

      const generation = stateGeneration
      this.markingIds = [...this.markingIds, notificationId]
      this.errorMessage = ''

      try {
        await notificationApi.markRead(notificationId)
        if (generation !== stateGeneration) return

        const latestNotice = this.notices.find((notice) => notice.notificationId === notificationId)
        const wasUnread = Boolean(latestNotice && !latestNotice.readTime)

        this.notices = this.notices.map((notice) =>
          notice.notificationId === notificationId
            ? { ...notice, readTime: new Date().toISOString() }
            : notice
        )
        if (wasUnread && this.unreadCount > 0) this.unreadCount -= 1
      } catch (error) {
        if (generation === stateGeneration) {
          this.errorMessage = getErrorMessage(error, '标记通知已读失败')
        }
        throw error
      } finally {
        if (generation === stateGeneration) {
          this.markingIds = this.markingIds.filter((id) => id !== notificationId)
        }
      }
    },

    async markBatchRead(notificationIds) {
      const normalizedIds = normalizeNotificationIds(notificationIds)
      const readIdSet = new Set(
        this.notices.filter((notice) => notice.readTime).map((notice) => notice.notificationId)
      )
      const markingIdSet = new Set(this.markingIds)
      const actionableIds = normalizedIds.filter(
        (notificationId) => !readIdSet.has(notificationId) && !markingIdSet.has(notificationId)
      )
      if (actionableIds.length === 0) return

      const generation = stateGeneration
      this.markingIds = [...this.markingIds, ...actionableIds]
      this.errorMessage = ''

      try {
        await notificationApi.markBatchRead(actionableIds)
        if (generation !== stateGeneration) return

        const notificationIdSet = new Set(actionableIds)
        const readTime = new Date().toISOString()
        const markedUnreadCount = this.notices.filter(
          (notice) => notificationIdSet.has(notice.notificationId) && !notice.readTime
        ).length

        this.notices = this.notices.map((notice) =>
          notificationIdSet.has(notice.notificationId) && !notice.readTime
            ? { ...notice, readTime }
            : notice
        )
        this.unreadCount = Math.max(0, this.unreadCount - markedUnreadCount)
      } catch (error) {
        if (generation === stateGeneration) {
          this.errorMessage = getErrorMessage(error, '批量标记通知已读失败')
        }
        throw error
      } finally {
        if (generation === stateGeneration) {
          const completedIdSet = new Set(actionableIds)
          this.markingIds = this.markingIds.filter((id) => !completedIdSet.has(id))
        }
      }
    },

    reset() {
      stateGeneration += 1
      latestFetchId += 1
      latestUnreadCountFetchId += 1
      this.$reset()
    }
  }
})
