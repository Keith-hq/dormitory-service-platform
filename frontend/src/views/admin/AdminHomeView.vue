<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { buildingApi } from '@/api/building'
import { InlineState, MetricStrip, StatusTag, WorkspaceHeader } from '@/components'
import { useNoticeStore } from '@/store/notice'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const loading = ref(true)
const error = ref('')
const failures = ref([])
const buildings = ref([])
const density = ref([])
const notices = ref([])
const noticeStore = useNoticeStore()
const userStore = useUserStore()

// 宿管负责楼栋（登录时 /auth/me 返回），首页头部展示
const myBuilding = computed(() => userStore.userInfo?.buildingName || '')
const myBuildingId = computed(() => {
  const raw = userStore.userInfo?.buildingId
  return raw ? Number(raw) : null
})

const totalInBuilding = computed(() =>
  density.value.reduce(
    (sum, item) => sum + Number(item.onlineCount ?? item.currentCount ?? item.count ?? 0),
    0
  )
)

const metrics = computed(() => [
  { label: '在管楼栋', value: buildings.value.length, hint: '空间底座' },
  {
    label: '当前在楼',
    value: totalInBuilding.value,
    hint: '实时在楼'
  },
  { label: '近期公告', value: notices.value.length, hint: '交接事项' },
  {
    label: '未读通知',
    value: String(noticeStore.unreadCount),
    hint: noticeStore.hasUnread ? '有未读消息' : '首页提醒'
  }
])

const syncLabel = computed(() => {
  if (loading.value) return '同步中'
  if (failures.value.length) return '部分数据不可用'
  return '数据已同步'
})

const syncTone = computed(() => {
  if (loading.value) return 'info'
  if (failures.value.length) return 'warning'
  return 'success'
})

const operationSummary = computed(() => {
  if (!buildings.value.length && !density.value.length) return '楼栋运营状态待同步'
  return `${buildings.value.length || density.value.length} 栋楼 · ${totalInBuilding.value} 人在楼 · ${notices.value.length} 条交接`
})

const pulseItems = computed(() =>
  (density.value.length ? density.value : buildings.value).slice(0, 6)
)

const quickLinks = [
  { index: '01', title: '空间档案', description: '楼栋、楼层与房间底账', to: '/building' },
  {
    index: '02',
    title: '住宿管理',
    description: '入住、床位与调宿记录',
    to: '/admin/accommodation'
  },
  { index: '03', title: '访客值守', description: '登记、核验与离场闭环', to: '/admin/duty' },
  { index: '04', title: '维修调度', description: '工单认领与处理进度', to: '/admin/repair' }
]

const formatDate = (value) => (value ? new Date(value).toLocaleDateString('zh-CN') : '—')

const load = async () => {
  loading.value = true
  error.value = ''
  failures.value = []
  try {
    const requests = [
      ['buildings', buildingApi.getList({ page: 1, pageSize: 100 })],
      ['density', adminApi.getAccessDensity()],
      ['notices', adminApi.getNotices({ page: 1, pageSize: 5 })]
    ]
    const results = await Promise.allSettled(requests.map(([, request]) => request))
    const targets = { buildings, density, notices }
    results.forEach((result, index) => {
      const key = requests[index][0]
      if (result.status === 'rejected') {
        failures.value.push(key)
        return
      }
      targets[key].value = normalizeCollection(result.value).items
    })
    // 宿管只统计/展示自己负责的楼栋（在管楼栋不再显示全站数量）
    if (myBuildingId.value) {
      buildings.value = buildings.value.filter(
        (building) => Number(building.buildingId) === myBuildingId.value
      )
    }
    const rejectedResults = results.filter((result) => result.status === 'rejected')
    if (rejectedResults.length === results.length)
      error.value = toUserMessage(rejectedResults[0].reason, '运营数据暂时无法同步')
  } finally {
    loading.value = false
  }

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

onMounted(load)
</script>

<template>
  <main class="admin-home workspace-page">
    <section class="home-portal">
      <WorkspaceHeader
        eyebrow="ADMIN / DAILY BRIEF"
        title="今日宿舍运营"
        description="楼栋承载、访客值守和公告交接集中在首页，异常事项优先显露。"
      >
        <span v-if="myBuilding" class="my-building">负责楼栋：{{ myBuilding }}</span>
        <StatusTag :label="syncLabel" :tone="syncTone" :dot="false" />
        <button class="btn btn-sm" type="button" :disabled="loading" @click="load">重新同步</button>
      </WorkspaceHeader>

      <section class="operations-card">
        <div>
          <span>OPERATIONS / CURRENT</span>
          <h2>{{ operationSummary }}</h2>
          <p>空间档案、住宿调整、访客登记与维修调度在同一张运营首页里快速进入。</p>
        </div>
        <router-link to="/admin/duty">进入访客值守台 <b>↗</b></router-link>
      </section>
    </section>

    <div class="metric-strip-wrap">
      <MetricStrip :metrics="metrics" style="--metric-count: 4" />
    </div>

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
            <span>BUILDING / PULSE</span>
            <h2>楼栋脉搏</h2>
          </div>
          <small>{{ pulseItems.length }} 条空间状态</small>
        </header>
        <InlineState
          :loading="loading"
          :error="
            failures.includes('density') && !pulseItems.length
              ? error || '楼栋脉搏暂时无法同步'
              : ''
          "
          :empty="!loading && !pulseItems.length"
          empty-text="暂无楼栋状态"
        />
        <article v-for="(item, index) in pulseItems" :key="item.buildingId ?? index">
          <time>0{{ index + 1 }}</time>
          <div>
            <h3>
              {{
                item.buildingName ??
                (item.buildingId ? `楼栋 ${item.buildingId}` : `楼栋 ${index + 1}`)
              }}
            </h3>
            <p>{{ item.buildingType ?? '实时在楼人数' }}</p>
          </div>
          <span>{{
            item.onlineCount ?? item.currentCount ?? item.count ?? item.floorCount ?? '—'
          }}</span>
        </article>
      </section>
    </div>

    <section class="handover-board">
      <header>
        <div>
          <span>HANDOVER / LATEST</span>
          <h2>公告与交接</h2>
        </div>
        <small>{{ notices.length }} 条近期公告</small>
      </header>
      <InlineState
        :loading="loading"
        :error="
          failures.includes('notices') && !notices.length ? error || '公告交接暂时无法同步' : ''
        "
        :empty="!loading && !notices.length"
        empty-text="暂无近期公告"
      />
      <article v-for="(notice, index) in notices" :key="notice.noticeId ?? index">
        <time>{{ formatDate(notice.createTime ?? notice.publishTime ?? notice.noticeTime) }}</time>
        <div>
          <h3>{{ notice.title ?? notice.noticeTitle ?? '宿舍通知' }}</h3>
          <p>{{ notice.content ?? notice.summary ?? '查看通知详情并纳入本班交接。' }}</p>
        </div>
        <span>{{ notice.noticeType ?? notice.category ?? '系统' }}</span>
      </article>
    </section>

    <section class="handover-board notice-center">
      <header>
        <div>
          <span>NOTIFY / CENTER</span>
          <h2>通知与已读</h2>
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
          <small>{{ noticeStore.notices.length }} 条通知</small>
        </div>
      </header>
      <InlineState
        :loading="noticeStore.loading"
        :error="noticeStore.errorMessage ? '通知暂时无法同步' : ''"
        :empty="!noticeStore.loading && !noticeStore.notices.length"
        empty-text="暂无通知"
      />
      <article
        v-for="item in noticeStore.notices"
        :key="item.notificationId"
        :class="{ unread: !item.readTime }"
      >
        <time>{{ formatDate(item.createTime) }}</time>
        <div>
          <h3>{{ item.title }}</h3>
          <p>{{ item.content }}</p>
        </div>
        <button
          v-if="!item.readTime"
          type="button"
          class="mark-read"
          :disabled="noticeStore.isMarking(item.notificationId)"
          @click="noticeStore.markRead(item.notificationId)"
        >
          标为已读
        </button>
        <span v-else>{{ item.notificationType || '系统' }}</span>
      </article>
    </section>
  </main>
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

.my-building {
  display: inline-flex;
  align-items: center;
  padding: 6px 12px;
  border: 1px solid rgba(255, 255, 255, 0.35);
  border-radius: 999px;
  background: rgba(255, 255, 255, 0.16);
  color: #fff;
  font-size: 13px;
  font-weight: 700;
  white-space: nowrap;
}

/* 指标条收窄并居中，避免横跨满宽导致各指标之间太开 */
.metric-strip-wrap {
  max-width: 820px;
  margin: 0 auto;
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

.home-portal :deep(.btn) {
  border-color: rgba(255, 255, 255, 0.42);
  background: rgba(255, 255, 255, 0.12);
  color: #fff;
}

.operations-card {
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
  color: #fff;
  gap: 24px;
  backdrop-filter: blur(12px);
}

.operations-card,
.operations-card * {
  color: #fff !important;
}

.operations-card span,
.notice-ledger header span,
.handover-board header span,
.quick-station header span {
  color: var(--color-accent-strong);
  font-size: 12px !important;
  font-weight: 950 !important;
  letter-spacing: 0 !important;
}

.operations-card span {
  color: #fff !important;
}

.operations-card h2 {
  margin: 8px 0 4px;
  color: #fff !important;
  font-family: var(--font-display);
  font-size: clamp(22px, 2.4vw, 30px);
  font-weight: 950;
  line-height: 1.16;
}

.operations-card p {
  margin: 0;
  color: #fff !important;
  font-size: 14px;
  line-height: 1.7;
}

.operations-card a {
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

.operations-card a b {
  color: #fff !important;
  font-size: 20px;
}

.admin-home :deep(.metric-strip) {
  position: relative;
  z-index: 3;
  width: min(100% - 72px, 1120px);
  margin: -32px auto 56px;
  border: 0;
  background: #fff;
  box-shadow: 0 22px 54px rgba(7, 58, 124, 0.11);
}

.admin-home :deep(.metric-strip article) {
  min-height: 118px;
  padding: 24px 30px;
}

.admin-home :deep(.metric-strip > article > span) {
  color: var(--color-accent-strong);
  font-size: 13px;
}

.admin-home :deep(.metric-strip strong) {
  font-size: clamp(32px, 3vw, 44px);
}

.admin-home :deep(.metric-strip small) {
  font-size: 14px;
  font-weight: 800;
}

.home-grid {
  display: grid;
  gap: 54px;
}

.notice-ledger,
.handover-board,
.quick-station {
  overflow: hidden;
  padding: 36px 42px 42px;
  border: 0 !important;
  border-radius: var(--radius-lg);
  background: #fff !important;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1) !important;
}

.notice-ledger > header,
.handover-board > header,
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

.notice-ledger h2,
.handover-board h2,
.quick-station h2 {
  margin: 8px 0 0;
  color: var(--color-ink) !important;
  font-family: var(--font-display);
  font-size: clamp(26px, 2.4vw, 36px) !important;
  font-weight: 950 !important;
  line-height: 1.15;
}

.notice-ledger header small,
.handover-board header small {
  flex: 0 0 auto;
  padding-right: 10px;
  color: var(--color-text-muted);
  font-size: 15px !important;
  font-weight: 700;
}

.quick-station {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 24px;
}

.quick-station > header {
  grid-column: 1/-1;
  margin-bottom: 30px;
}

.quick-station > a {
  position: relative;
  display: grid;
  min-height: 118px;
  overflow: hidden;
  padding: 24px 28px;
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

.notice-ledger {
  display: grid;
  grid-template-columns: 1fr;
  align-items: start;
}

.notice-ledger :deep(.inline-state),
.handover-board :deep(.inline-state) {
  margin-bottom: 14px;
}

.notice-ledger article,
.handover-board article {
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

.notice-ledger article:hover,
.handover-board article:hover {
  background: #eef4ff;
}

.notice-ledger article > span,
.handover-board article > span {
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

.notice-ledger article h3,
.handover-board article h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
  line-height: 1.35;
}

.notice-ledger article p,
.handover-board article p {
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

.notice-ledger article time,
.handover-board article time {
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

.handover-board {
  margin-top: 54px;
}

.notice-actions {
  display: flex;
  align-items: center;
  flex-wrap: wrap;
  gap: 12px;
}
.notice-actions small {
  color: var(--color-text-muted);
  font-size: 15px !important;
  font-weight: 700;
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

@media (max-width: 1180px) {
  .quick-station {
    grid-template-columns: repeat(2, minmax(0, 1fr));
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

  .operations-card {
    align-items: stretch;
    flex-direction: column;
    margin: 10px 28px 34px;
    padding: 22px;
  }

  .operations-card a {
    width: 100%;
  }

  .admin-home :deep(.metric-strip) {
    width: 100%;
    margin: 22px 0 44px;
  }

  .quick-station {
    grid-template-columns: 1fr;
  }

  .notice-ledger,
  .handover-board,
  .quick-station {
    padding: 30px 24px 34px;
  }

  .notice-ledger > header,
  .handover-board > header,
  .quick-station > header {
    align-items: flex-start;
    flex-direction: column;
  }

  .notice-ledger h2,
  .handover-board h2,
  .quick-station h2 {
    font-size: clamp(30px, 10vw, 40px) !important;
  }

  .notice-ledger article,
  .handover-board article {
    grid-template-columns: 1fr;
    gap: 14px;
  }

  .notice-ledger article time,
  .handover-board article time {
    justify-items: start;
    padding-left: 12px;
  }
}
</style>
