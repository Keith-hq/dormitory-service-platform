<script setup>
import { computed, onMounted, ref } from 'vue'
import { notificationApi } from '@/api/notification'
import { studentApi } from '@/api/student'
import { InlineState, MetricStrip, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

const userStore = useUserStore()
const loading = ref(true)
const failures = ref([])
const wallet = ref(null)
const fees = ref([])
const accommodation = ref(null)
const notifications = ref([])

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
    value: String(notifications.value.filter((item) => !item.readTime).length),
    hint: '首页提醒'
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
    ['accommodation', studentApi.getAccommodation(studentId.value)],
    ['notifications', notificationApi.getList({ page: 1, pageSize: 5 })]
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
    if (key === 'notifications') notifications.value = normalizeCollection(result.value).items
  })
  loading.value = false
}

onMounted(loadHome)
</script>

<template>
  <div class="student-home workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / DAILY BRIEF"
      :title="`${userStore.userName || '同学'}，今天从这里开始`"
      description="通知、费用和最常用的宿舍服务集中在首页，异常事项优先显示。"
    >
      <StatusTag v-if="loading" label="同步中" tone="info" />
      <StatusTag v-else-if="failures.length" label="部分数据不可用" tone="warning" />
      <StatusTag v-else label="数据已同步" tone="success" />
    </WorkspaceHeader>

    <section class="residence-brief">
      <div>
        <span>RESIDENCE / CURRENT</span>
        <h2>{{ residence }}</h2>
        <p>住宿档案与床位状态由宿管端统一维护。</p>
      </div>
      <router-link to="/student/profile">查看住宿档案 <b>↗</b></router-link>
    </section>

    <MetricStrip :metrics="metrics" />

    <div class="home-grid">
      <section class="notice-ledger">
        <header>
          <div>
            <span>NOTICE / LATEST</span>
            <h2>通知与提醒</h2>
          </div>
          <small>{{ notifications.length }} 条最新消息</small>
        </header>
        <InlineState
          :loading="loading"
          :error="failures.includes('notifications') ? '通知暂时无法同步' : ''"
          :empty="!loading && !notifications.length"
          empty-text="暂无通知"
        />
        <article
          v-for="notice in notifications"
          :key="notice.notificationId"
          :class="{ unread: !notice.readTime }"
        >
          <span>{{ notice.notificationType || '系统' }}</span>
          <div>
            <h3>{{ notice.title }}</h3>
            <p>{{ notice.content }}</p>
          </div>
          <time>{{
            notice.createTime ? new Date(notice.createTime).toLocaleDateString('zh-CN') : '—'
          }}</time>
        </article>
      </section>

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
    </div>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.residence-brief {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 26px 0;
  padding: 22px 25px;
  background: var(--color-ink);
  color: var(--color-paper);
  gap: 20px;
}
.residence-brief span,
.notice-ledger header span,
.quick-station header span {
  color: #d98568;
  font: 8px var(--font-mono);
  letter-spacing: 0.16em;
}
.residence-brief h2 {
  margin: 7px 0 4px;
  font-family: var(--font-display);
  font-size: 22px;
  font-weight: 500;
}
.residence-brief p {
  margin: 0;
  color: #98aaa0;
  font-size: 10px;
}
.residence-brief a {
  min-width: 150px;
  padding: 11px 13px;
  border: 1px solid #66796e;
  color: #f5edde;
  font-size: 10px;
  text-decoration: none;
}
.residence-brief a b {
  float: right;
  color: #e19575;
}
.home-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(280px, 0.6fr);
  margin-top: 27px;
  gap: 18px;
}
.notice-ledger,
.quick-station {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.55);
}
.notice-ledger > header,
.quick-station > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 17px 20px;
  border-bottom: 1px solid var(--color-line);
}
.notice-ledger h2,
.quick-station h2 {
  margin: 5px 0 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 500;
}
.notice-ledger header small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.notice-ledger article {
  display: grid;
  grid-template-columns: 44px 1fr auto;
  align-items: center;
  min-height: 79px;
  padding: 13px 20px;
  border-bottom: 1px solid var(--color-line);
  gap: 13px;
}
.notice-ledger article > span {
  padding: 4px 5px;
  border: 1px solid var(--color-brand-border);
  color: var(--color-brand);
  font-size: 8px;
  text-align: center;
}
.notice-ledger article h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 13px;
}
.notice-ledger article p {
  overflow: hidden;
  margin: 5px 0 0;
  color: var(--color-text-muted);
  font-size: 9px;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.notice-ledger article time {
  color: var(--color-text-soft);
  font: 8px var(--font-mono);
}
.notice-ledger article.unread {
  box-shadow: inset 3px 0 var(--color-accent);
}
.quick-station > header {
  min-height: 70px;
}
.quick-station > a {
  display: grid;
  grid-template-columns: 27px 1fr auto;
  align-items: center;
  min-height: 76px;
  padding: 13px 18px;
  border-bottom: 1px solid var(--color-line);
  color: inherit;
  text-decoration: none;
  gap: 10px;
}
.quick-station > a:hover {
  background: var(--color-brand-soft);
}
.quick-station > a > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.quick-station > a div {
  display: grid;
  gap: 5px;
}
.quick-station > a b {
  font-family: var(--font-display);
  font-size: 13px;
}
.quick-station > a small {
  color: var(--color-text-muted);
  font-size: 8px;
}
.quick-station > a i {
  color: var(--color-accent);
  font-style: normal;
}
@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .home-grid {
    grid-template-columns: 1fr;
  }
  .residence-brief {
    align-items: start;
    flex-direction: column;
  }
  .residence-brief a {
    width: 100%;
  }
}
</style>
