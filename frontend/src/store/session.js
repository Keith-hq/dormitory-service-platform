import { useUserStore } from '@/store/user'

/**
 * 清理当前会话关联的全部 Pinia 状态。
 * notice store 使用动态导入，避免通知模块进入首屏公共包。
 */
export const clearSession = async () => {
  useUserStore().logout()

  const { useNoticeStore } = await import('@/store/notice')
  useNoticeStore().reset()
}
