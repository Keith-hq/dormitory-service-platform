<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, MetricStrip, StatusTag, WorkspaceHeader } from '@/components'
import { useNoticeStore } from '@/store/notice'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

const userStore = useUserStore()
const noticeStore = useNoticeStore()
const loading = ref(true)
const failures = ref([])
const wallet = ref(null)
const fees = ref([])
const accommodation = ref(null)

const studentId = computed(() => userStore.userInfo?.id || '')
const unpaidFees = computed(() => fees.value.filter((item) => item.isPaid !== '是'))
const unpaidTotal = computed(() =>
  unpaidFees.value.reduce((sum, item) => sum + Number(item.total || 0), 0)
)
const residence = computed(() => {
  if (!accommodation.value) return userStore.userInfo?.buildingName || '住宿信息待同步'
  return `${userStore.userInfo?.buildingName || '宿舍楼'} · 房间 ${accommodation.value.roomId} · ${accommodation.value.bedNo} 号床`
})
const metrics = computed(() => [
  {
    label: '钱包余额',
    value: wallet.value ? `¥${Number(wallet.value.balance).toFixed(2)}` : '—',
    hint: wallet.value?.lowBalanceWarning ? '余额偏低' : '状态正常'
  },
  {
    label: '待缴费用',
    value: `¥${unpaidTotal.value.toFixed(2)}`,
    hint: `${unpaidFees.value.length} 笔账单`
  },
  {
    label: '未读通知',
    value: String(noticeStore.unreadCount),
    hint: noticeStore.hasUnread ? '有未读消息' : '首页提醒'
  }
])

const quickLinks = [
  { index: '01', title: '查看费用', description: '账单、分摊与钱包流水', to: '/student/finance' },
  { index: '02', title: '发起报修', description: '提交问题并跟踪处理进度', to: '/student/repair' },
  {
    index: '03',
    title: '预约设施',
    description: '查看可用时段与共享物品',
    to: '/student/facilities'
  },
  {
    index: '04',
    title: '安全社区',
    description: '晚归、离校、访客与信用',
    to: '/student/community'
  }
]

const loadHome = async () => {
  loading.value = true
  failures.value = []
  const requests = [
    ['wallet', studentApi.getWallet(studentId.value)],
    ['fees', studentApi.getFees(studentId.value)],
    ['accommodation', studentApi.getAccommodation(studentId.value)]
  ]
  const results = await Promise.allSettled(requests.map(([, request]) => request))
  results.forEach((result, index) => {
    const key = requests[index][0]
    if (result.status === 'rejected') {
      failures.value.push(key)
      return
    }
    if (key === 'wallet') wallet.value = result.value
    if (key === 'fees') fees.value = normalizeCollection(result.value).items
    if (key === 'accommodation') accommodation.value = result.value
  })
  loading.value = false
  noticeStore.fetchNotices({ page: 1, pageSize: 5 }).catch(() => {})
  noticeStore.fetchUnreadCount().catch(() => {})
}

const markAllRead = () => {
  const unreadIds = noticeStore.notices
    .filter((item) => !item.readTime)
    .map((item) => item.notificationId)
  if (unreadIds.length) {
    noticeStore.markBatchRead(unreadIds).catch(() => {})
  }
}

onMounted(loadHome)
</script>

<template>
  <div class="student-home workspace-page">
    <section class="home-portal">
      <WorkspaceHeader
        eyebrow="STUDENT / DAILY BRIEF"
        :title="`${userStore.userName || '同学'}，今天从这里开始`"
        description="通知、费用和最常用的宿舍服务集中在首页，异常事项优先显示。"
      >
        <StatusTag v-if="loading" label="同步中" tone="info" :dot="false" />
        <StatusTag v-else-if="failures.length" label="部分数据不可用" tone="warning" :dot="false" />
        <StatusTag v-else label="数据已同步" tone="success" :dot="false" />
      </WorkspaceHeader>

      <section class="student-residence-card">
        <div>
          <span>RESIDENCE / CURRENT</span>
          <h2>{{ residence }}</h2>
          <p>住宿档案与床位状态由宿管端统一维护。</p>
        </div>
        <router-link to="/student/profile">查看住宿档案 <b>↗</b></router-link>
      </section>
    </section>

    <MetricStrip :metrics="metrics" />

    <div class="home-grid">
      <aside class="quick-station">
        <header>
          <span>QUICK ACCESS</span>
          <h2>常用入口</h2>
        </header>
        <router-link v-for="item in quickLinks" :key="item.to" :to="item.to">
          <span>{{ item.index }}</span>
          <div>
            <b>{{ item.title }}</b
            ><small>{{ item.description }}</small>
          </div>
          <i>↗</i>
        </router-link>
      </aside>

      <section class="notice-ledger">
        <header>
          <div>
            <span>NOTICE / LATEST</span>
            <h2>通知与提醒</h2>
          </div>
          <div class="notice-actions">
            <b v-if="noticeStore.hasUnread" class="unread-badge">{{ noticeStore.unreadCount }}</b>
            <button
              type="button"
              class="mark-all"
              :disabled="!noticeStore.hasUnread"
              @click="markAllRead"
            >
              全部已读
            </button>
            <small>{{ noticeStore.notices.length }} 条最新消息</small>
          </div>
        </header>
        <InlineState
          :loading="noticeStore.loading"
          :error="noticeStore.errorMessage ? '通知暂时无法同步' : ''"
          :empty="!noticeStore.loading && !noticeStore.notices.length"
          empty-text="暂无通知"
        />
        <article
          v-for="notice in noticeStore.notices"
          :key="notice.notificationId"
          :class="{ unread: !notice.readTime }"
        >
          <time>{{
            notice.createTime ? new Date(notice.createTime).toLocaleDateString('zh-CN') : '—'
          }}</time>
          <div>
            <h3>{{ notice.title }}</h3>
            <p>{{ notice.content }}</p>
          </div>
          <button
            v-if="!notice.readTime"
            type="button"
            class="mark-read"
            :disabled="noticeStore.isMarking(notice.notificationId)"
            @click="noticeStore.markRead(notice.notificationId)"
          >
            标为已读
          </button>
          <span v-else>{{ notice.notificationType || '系统' }}</span>
        </article>
      </section>
    </div>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 64px, 1280px);
  margin: 0 auto;
  padding: 34px 0 82px;
}

.home-portal {
  position: relative;
  overflow: hidden;
  min-height: 360px;
  border-radius: var(--radius-lg);
  background:
    linear-gradient(90deg, rgba(6, 54, 142, 0.96), rgba(13, 96, 198, 0.9)),
    linear-gradient(135deg, #073f9b, #0a82c2);
  color: #fff;
  box-shadow: 0 24px 66px rgba(6, 58, 135, 0.18);
}

.home-portal::before {
  position: absolute;
  inset: 0;
  background:
    radial-gradient(circle at 83% 12%, rgba(255, 255, 255, 0.2), transparent 26%),
    linear-gradient(90deg, rgba(255, 255, 255, 0.09) 1px, transparent 1px) 0 0 / 72px 72px,
    linear-gradient(180deg, rgba(255, 255, 255, 0.07) 1px, transparent 1px) 0 0 / 72px 72px;
  content: '';
  opacity: 0.68;
}

.home-portal::after {
  position: absolute;
  right: -96px;
  bottom: -150px;
  width: 380px;
  height: 380px;
  border: 52px solid rgba(255, 255, 255, 0.13);
  border-radius: 50%;
  content: '';
}

.home-portal :deep(.workspace-header) {
  position: relative;
  z-index: 1;
  display: grid;
  min-height: 0;
  padding: 46px 54px 18px;
  color: #fff;
}

.home-portal :deep(.workspace-header::before) {
  content: none !important;
}

.home-portal :deep(.workspace-header h1) {
  max-width: 760px;
  color: #fff;
  font-size: clamp(36px, 4.6vw, 58px);
  font-weight: 950;
  line-height: 1.08;
}

.home-portal :deep(.workspace-header__description) {
  max-width: 680px;
  margin-top: 18px;
  color: rgba(255, 255, 255, 0.86);
  font-size: 17px;
  font-weight: 700;
  line-height: 1.8;
}

.home-portal :deep(.workspace-header__aside) {
  justify-self: start;
  margin-top: 18px;
}

.home-portal :deep(.status-tag) {
  border-color: rgba(255, 255, 255, 0.42);
  background: rgba(255, 255, 255, 0.14);
  color: #fff;
}

.student-residence-card {
  position: relative;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: space-between;
  overflow: hidden;
  min-height: 96px;
  margin: 10px 54px 42px;
  padding: 20px 24px;
  border: 1px solid rgba(255, 255, 255, 0.22);
  background: rgba(255, 255, 255, 0.12);
  color: #fff !important;
  gap: 24px;
  backdrop-filter: blur(12px);
}
.student-residence-card,
.student-residence-card * {
  color: #fff !important;
}
.student-residence-card::before {
  content: none;
}
.student-residence-card span,
.notice-ledger header span,
.quick-station header span {
  color: var(--color-accent-strong);
  font-size: 12px !important;
  font-weight: 950 !important;
  letter-spacing: 0 !important;
}
.student-residence-card span {
  color: #fff !important;
}
.student-residence-card h2 {
  margin: 8px 0 4px;
  color: #fff !important;
  font-family: var(--font-display);
  font-size: clamp(22px, 2.4vw, 30px);
  font-weight: 950;
  line-height: 1.16;
}
.student-residence-card p {
  margin: 0;
  color: #fff !important;
  font-size: 14px;
  line-height: 1.7;
}
.student-residence-card a {
  display: inline-flex;
  align-items: center;
  justify-content: space-between;
  min-width: 184px;
  min-height: 52px;
  padding: 0 18px;
  border: 1px solid rgba(255, 255, 255, 0.42);
  background: rgba(255, 255, 255, 0.12);
  color: #fff !important;
  font-size: 15px;
  font-weight: 900;
  text-decoration: none;
}
.student-residence-card a b {
  color: #fff !important;
  font-size: 20px;
}

:global(#app) .student-home .student-residence-card,
:global(#app) .student-home .student-residence-card span,
:global(#app) .student-home .student-residence-card h2,
:global(#app) .student-home .student-residence-card p,
:global(#app) .student-home .student-residence-card a,
:global(#app) .student-home .student-residence-card b {
  color: #fff !important;
}

.student-home :deep(.metric-strip) {
  position: relative;
  z-index: 3;
  width: min(100% - 72px, 1120px);
  margin: -32px auto 56px;
  border: 0;
  background: #fff;
  box-shadow: 0 22px 54px rgba(7, 58, 124, 0.11);
}

.student-home :deep(.metric-strip article) {
  min-height: 118px;
  padding: 24px 30px;
}

.student-home :deep(.metric-strip > article > span) {
  color: var(--color-accent-strong);
  font-size: 13px;
}

.student-home :deep(.metric-strip strong) {
  font-size: clamp(32px, 3vw, 44px);
}

.student-home :deep(.metric-strip small) {
  font-size: 14px;
  font-weight: 800;
}

.home-grid {
  display: grid;
  gap: 54px;
}
.notice-ledger,
.quick-station {
  overflow: hidden;
  padding: 36px 42px 42px;
  border: 0 !important;
  border-radius: var(--radius-lg);
  background: #fff !important;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1) !important;
}
.notice-ledger > header,
.quick-station > header {
  position: relative;
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  min-height: 0;
  padding: 0 0 28px;
  border-bottom: 0;
  background: transparent;
  gap: 22px;
}
.notice-ledger > header::after,
.quick-station > header::after {
  content: none;
}
.notice-ledger h2,
.quick-station h2 {
  margin: 8px 0 0;
  font-family: var(--font-display);
  font-size: clamp(26px, 2.4vw, 36px) !important;
  font-weight: 950 !important;
  line-height: 1.15;
}
.notice-ledger header small,
.quick-station header small {
  color: var(--color-text-muted);
  font-size: 15px !important;
  font-weight: 700;
}

.notice-ledger > header > div,
.quick-station > header > div {
  min-width: 0;
}

.notice-ledger > header > small {
  flex: 0 0 auto;
  padding-right: 10px;
}
.notice-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}
.unread-badge {
  display: grid;
  min-width: 24px;
  height: 24px;
  place-items: center;
  border-radius: 999px;
  background: var(--color-danger);
  color: #fff;
  font: 13px var(--font-mono);
  font-weight: 900;
}
.mark-all,
.mark-read {
  min-height: 32px;
  padding: 0 12px;
  border: 1px solid var(--color-brand-border);
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 13px;
  font-weight: 850;
  cursor: pointer;
}
.mark-read {
  border-color: var(--color-line-strong);
  background: var(--color-surface);
}
.mark-all:disabled,
.mark-read:disabled {
  opacity: 0.55;
  cursor: not-allowed;
}

.quick-station {
  display: grid;
}

.quick-station > header {
  margin-bottom: 30px;
}

.quick-station > a {
  position: relative;
  display: grid;
  min-height: 118px;
  padding: 24px 28px;
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  color: inherit;
  text-decoration: none;
  box-shadow: 0 12px 24px rgba(23, 65, 120, 0.08);
  transition:
    transform 0.18s ease,
    box-shadow 0.18s ease;
}

.quick-station > a::before {
  content: none;
}

.quick-station > a:hover {
  transform: translateY(-4px);
  box-shadow: 0 24px 46px rgba(7, 58, 124, 0.13);
}

.quick-station > a > span {
  display: grid;
  width: 42px;
  height: 42px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font: 15px var(--font-mono);
  font-weight: 950;
}

.quick-station > a div {
  display: grid;
  align-content: start;
  gap: 10px;
  padding: 18px 0 0;
}

.quick-station > a b {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 950;
  line-height: 1.24;
}

.quick-station > a small {
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 650;
  line-height: 1.7;
}

.quick-station > a i {
  position: absolute;
  right: 22px;
  bottom: 22px;
  color: var(--color-brand);
  font-size: 22px;
  font-style: normal;
}

.quick-station {
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
}

.quick-station > header {
  grid-column: 1/-1;
}

.notice-ledger {
  display: grid;
  grid-template-columns: 1fr;
  align-items: start;
}

.notice-ledger > header {
  grid-column: 1/-1;
}

.notice-ledger :deep(.inline-state) {
  grid-column: 1/-1;
}

.notice-ledger article {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr) auto;
  align-items: center;
  min-height: 108px;
  margin-top: 14px;
  padding: 22px 24px;
  border-bottom: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  gap: 24px;
}
.notice-ledger article:hover {
  background: #eef4ff;
}
.notice-ledger article > span {
  display: grid;
  min-width: 60px;
  min-height: 34px;
  place-items: center;
  border: 1px solid var(--color-brand-border);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 13px;
  font-weight: 900;
  text-align: center;
}
.notice-ledger article h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
  line-height: 1.35;
}
.notice-ledger article p {
  display: -webkit-box;
  overflow: hidden;
  margin: 8px 0 0;
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 600;
  line-height: 1.7;
  -webkit-box-orient: vertical;
  -webkit-line-clamp: 2;
}
.notice-ledger article time {
  display: grid;
  min-height: 62px;
  place-items: center;
  border-left: 0;
  border-radius: var(--radius-lg);
  background: #edf5ff;
  color: var(--color-brand-strong);
  font: 15px var(--font-mono);
  font-weight: 950;
}
.notice-ledger article.unread {
  box-shadow: none;
}

@media (max-width: 1180px) {
  .quick-station {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .notice-ledger {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
    padding-top: 28px;
  }
  .home-portal :deep(.workspace-header) {
    padding: 36px 28px 18px;
  }
  .home-portal :deep(.workspace-header aside) {
    justify-self: start;
    margin-top: 12px;
  }
  .notice-ledger > header,
  .quick-station > header {
    align-items: flex-start;
    flex-direction: column;
  }
  .notice-ledger h2,
  .quick-station h2 {
    font-size: clamp(30px, 10vw, 40px) !important;
  }
  .student-residence-card {
    align-items: stretch;
    grid-template-columns: 1fr;
    flex-direction: column;
    margin: 10px 28px 34px;
    padding: 22px;
  }
  .student-home :deep(.metric-strip) {
    width: 100%;
    margin: 22px 0 44px;
  }
  .quick-station {
    grid-template-columns: 1fr;
  }
  .notice-ledger,
  .quick-station {
    padding: 30px 24px 34px;
  }
  .student-residence-card a {
    width: 100%;
  }
  .notice-ledger article {
    grid-template-columns: 1fr;
    gap: 14px;
  }
  .notice-ledger article time {
    justify-items: start;
    padding-left: 12px;
  }
}
</style>
