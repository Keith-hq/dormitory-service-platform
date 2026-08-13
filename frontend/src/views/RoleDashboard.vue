<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { buildingApi } from '@/api/building'
import { studentApi } from '@/api/student'
import { PageHeader, StatusTag } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

const userStore = useUserStore()
const loading = ref(true)
const failedSections = ref([])
const collections = ref({
  waterOrders: [],
  packages: [],
  appeals: [],
  accessLogs: [],
  violations: [],
  buildings: []
})

const isStudent = computed(() => userStore.userInfo?.role === 'student')
const dutyDate = new Intl.DateTimeFormat('zh-CN', {
  month: '2-digit',
  day: '2-digit'
}).format(new Date())
const residence = computed(() => {
  const buildingName = userStore.userInfo?.buildingName || '宿舍园区'
  const roomName = userStore.userInfo?.roomName
  return roomName ? `${buildingName} · ${roomName}` : buildingName
})

const pageCopy = computed(() =>
  isStudent.value
    ? {
        eyebrow: 'MY CAMPUS DAY',
        title: `早上好，${userStore.userName}`,
        description: '把订水、快递与信用申诉收拢到一处，今天需要处理的生活事项一眼可见。'
      }
    : {
        eyebrow: `DUTY DESK · ${dutyDate}`,
        title: `${userStore.userName}，今日值班已开始`,
        description: '按紧急程度查看配送、门禁异常与违规记录，保持楼栋事务有序流转。'
      }
)

const studentMetrics = computed(() => [
  {
    label: '待取快递',
    value: collections.value.packages.filter((item) => item.status === 'ready').length,
    suffix: '件'
  },
  {
    label: '进行中订水',
    value: collections.value.waterOrders.filter((item) => item.status !== 'completed').length,
    suffix: '单'
  },
  {
    label: '信用状态',
    value: 96,
    suffix: '分'
  }
])

const adminMetrics = computed(() => [
  {
    label: '待配送',
    value: collections.value.waterOrders.filter((item) => item.status === 'pending').length,
    suffix: '单'
  },
  {
    label: '门禁异常',
    value: collections.value.accessLogs.filter((item) => item.status !== 'normal').length,
    suffix: '条'
  },
  {
    label: '待处理违规',
    value: collections.value.violations.filter((item) => item.status === 'pending').length,
    suffix: '项'
  },
  { label: '在管楼栋', value: collections.value.buildings.length, suffix: '栋' }
])

const metrics = computed(() => (isStudent.value ? studentMetrics.value : adminMetrics.value))

const modules = computed(() => {
  if (isStudent.value) {
    return [
      {
        index: '01',
        title: '订水服务',
        description: '查看配送进度，按宿舍需求订购 1–4 桶饮用水。',
        to: { path: '/student/services', query: { tab: 'water' } },
        count: `${collections.value.waterOrders.length} 条记录`
      },
      {
        index: '02',
        title: '快递取件',
        description: '查看货架位置与取件码，到站后在线确认取件。',
        to: { path: '/student/services', query: { tab: 'packages' } },
        count: `${collections.value.packages.filter((item) => item.status === 'ready').length} 件待取`
      },
      {
        index: '03',
        title: '信用申诉',
        description: '追踪信用分异议的复核状态与处理结果。',
        to: { path: '/student/services', query: { tab: 'appeals' } },
        count: `${collections.value.appeals.length} 条申诉`
      }
    ]
  }

  return [
    {
      index: '01',
      title: '配送调度',
      description: '按下单时间处理订水，记录配送与送达节点。',
      to: { path: '/admin/operations', query: { tab: 'water' } },
      count: `${collections.value.waterOrders.filter((item) => item.status === 'pending').length} 单待配送`
    },
    {
      index: '02',
      title: '门禁巡查',
      description: '查看实时进出记录，优先核对晚归等异常事件。',
      to: { path: '/admin/operations', query: { tab: 'access' } },
      count: `${collections.value.accessLogs.filter((item) => item.status !== 'normal').length} 条异常`
    },
    {
      index: '03',
      title: '违规台账',
      description: '集中查看违规记录与信用分变动，跟进待处理事项。',
      to: { path: '/admin/operations', query: { tab: 'violations' } },
      count: `${collections.value.violations.length} 条记录`
    },
    {
      index: '04',
      title: '楼栋档案',
      description: '维护楼栋类型、楼层数量与基础空间档案。',
      to: '/building',
      count: `${collections.value.buildings.length} 栋在册`
    }
  ]
})

const statusLabel = {
  pending: '待处理',
  delivering: '配送中',
  completed: '已完成',
  ready: '待取件',
  picked_up: '已取件',
  reviewing: '复核中',
  approved: '已通过',
  normal: '正常',
  late: '晚归',
  processed: '已处理'
}

const statusTone = (status) => {
  if (['completed', 'picked_up', 'approved', 'normal', 'processed'].includes(status))
    return 'success'
  if (['late'].includes(status)) return 'danger'
  if (['delivering', 'reviewing'].includes(status)) return 'info'
  return 'warning'
}

const activity = computed(() => {
  if (isStudent.value) {
    const packageItems = collections.value.packages
      .filter((item) => item.status === 'ready')
      .map((item) => ({
        id: `package-${item.packageId}`,
        title: `${item.courierCompany}已到站`,
        detail: `${item.shelfCode} · 取件码 ${item.pickupCode}`,
        status: item.status,
        time: item.arrivedAt
      }))
    const orderItems = collections.value.waterOrders.slice(0, 2).map((item) => ({
      id: `water-${item.orderId}`,
      title: `${item.quantity} 桶饮用水订单`,
      detail: `订单 #${item.orderId} · ¥${item.amount}`,
      status: item.status,
      time: item.createdAt
    }))
    return [...packageItems, ...orderItems].slice(0, 4)
  }

  const lateItems = collections.value.accessLogs
    .filter((item) => item.status !== 'normal')
    .map((item) => ({
      id: `access-${item.logId}`,
      title: `${item.studentName} · ${item.roomName} 室`,
      detail: `${item.direction}记录需要核对`,
      status: item.status,
      time: item.accessTime
    }))
  const orderItems = collections.value.waterOrders
    .filter((item) => item.status === 'pending')
    .map((item) => ({
      id: `water-${item.orderId}`,
      title: `${item.roomName} · ${item.quantity} 桶水`,
      detail: `${item.studentName}的订单待配送`,
      status: item.status,
      time: item.createdAt
    }))
  return [...lateItems, ...orderItems].slice(0, 4)
})

const readItems = (payload) => normalizeCollection(payload).items

const loadDashboard = async () => {
  loading.value = true
  failedSections.value = []
  const studentId = userStore.userInfo?.id
  const requests = isStudent.value
    ? [
        ['waterOrders', studentApi.getWaterOrders(studentId)],
        ['packages', studentApi.getPackages(studentId)],
        ['appeals', studentApi.getCreditAppeals(studentId)]
      ]
    : [
        ['waterOrders', adminApi.getWaterOrders()],
        ['accessLogs', adminApi.getAccessLogs()],
        ['violations', adminApi.getViolations()],
        ['buildings', buildingApi.getList({ page: 1, pageSize: 100 })]
      ]

  const results = await Promise.allSettled(requests.map(([, request]) => request))
  results.forEach((result, index) => {
    const key = requests[index][0]
    if (result.status === 'fulfilled') {
      collections.value[key] = readItems(result.value)
    } else {
      failedSections.value.push(key)
    }
  })
  loading.value = false
}

onMounted(loadDashboard)
</script>

<template>
  <div class="dashboard-page">
    <PageHeader v-bind="pageCopy">
      <StatusTag v-if="loading" label="同步中" tone="info" />
      <StatusTag v-else-if="failedSections.length" label="部分数据暂不可用" tone="warning" />
      <StatusTag v-else label="数据已同步" tone="success" />
    </PageHeader>

    <section class="identity-card" :class="{ 'identity-card--admin': !isStudent }">
      <div class="identity-card__copy">
        <span>{{ isStudent ? 'RESIDENCE PASS' : 'ON-DUTY CONSOLE' }}</span>
        <strong>{{ residence }}</strong>
        <p>
          {{
            isStudent
              ? '你的宿舍生活事项会按状态自动整理。'
              : '当前视图聚焦桂苑 A 栋的今日待办与异常记录。'
          }}
        </p>
      </div>
      <div class="metric-strip" aria-label="今日数据概览">
        <div v-for="metric in metrics" :key="metric.label" class="metric-strip__item">
          <span>{{ metric.label }}</span>
          <strong
            >{{ loading ? '—' : metric.value }}<small>{{ metric.suffix }}</small></strong
          >
        </div>
      </div>
    </section>

    <section class="dashboard-section" aria-labelledby="module-heading">
      <div class="section-heading">
        <div>
          <span>ROLE WORKSPACE</span>
          <h2 id="module-heading">{{ isStudent ? '我的生活服务' : '值班功能入口' }}</h2>
        </div>
        <small>按角色展示 · Apifox 契约驱动</small>
      </div>

      <div class="module-grid" :class="{ 'module-grid--admin': !isStudent }">
        <router-link
          v-for="module in modules"
          :key="module.index"
          :to="module.to"
          class="module-card"
        >
          <span class="module-card__index">{{ module.index }}</span>
          <div>
            <h3>{{ module.title }}</h3>
            <p>{{ module.description }}</p>
          </div>
          <footer>
            <span>{{ module.count }}</span>
            <strong aria-hidden="true">↗</strong>
          </footer>
        </router-link>
      </div>
    </section>

    <section class="activity-board" aria-labelledby="activity-heading">
      <header>
        <div>
          <span>LIVE QUEUE</span>
          <h2 id="activity-heading">{{ isStudent ? '最近动态' : '优先处理队列' }}</h2>
        </div>
        <router-link :to="isStudent ? '/student/services' : '/admin/operations'">
          查看全部 <span aria-hidden="true">→</span>
        </router-link>
      </header>

      <div v-if="loading" class="activity-empty" role="status">正在整理角色数据…</div>
      <div v-else-if="activity.length" class="activity-list">
        <article v-for="item in activity" :key="item.id" class="activity-item">
          <span class="activity-item__line" aria-hidden="true"></span>
          <div>
            <strong>{{ item.title }}</strong>
            <p>{{ item.detail }}</p>
          </div>
          <StatusTag
            :label="statusLabel[item.status] || item.status"
            :tone="statusTone(item.status)"
          />
          <time>{{ item.time }}</time>
        </article>
      </div>
      <div v-else class="activity-empty">当前没有待处理动态</div>
    </section>
  </div>
</template>

<style scoped>
.dashboard-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}

.identity-card {
  position: relative;
  display: grid;
  grid-template-columns: minmax(260px, 0.85fr) minmax(0, 1.4fr);
  overflow: hidden;
  margin-top: var(--space-7);
  border-radius: 2px 24px 2px 24px;
  background:
    radial-gradient(circle at 12% 18%, rgba(255, 255, 255, 0.16), transparent 24%),
    linear-gradient(132deg, #173136, #126a65);
  color: #fff;
  box-shadow: var(--shadow-lift);
}

.identity-card--admin {
  background:
    linear-gradient(90deg, rgba(255, 255, 255, 0.035) 1px, transparent 1px),
    linear-gradient(132deg, #202e32, #475c58);
  background-size:
    32px 32px,
    auto;
}

.identity-card::after {
  content: '';
  position: absolute;
  right: -60px;
  bottom: -145px;
  width: 310px;
  height: 310px;
  border: 1px solid rgba(255, 255, 255, 0.13);
  border-radius: 50%;
  box-shadow: 0 0 0 50px rgba(255, 255, 255, 0.035);
}

.identity-card__copy {
  position: relative;
  z-index: 1;
  padding: clamp(28px, 4vw, 46px);
}

.identity-card__copy > span,
.section-heading > div > span,
.activity-board header > div > span {
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.18em;
}

.identity-card__copy > span {
  color: rgba(255, 255, 255, 0.62);
}

.identity-card__copy strong {
  display: block;
  margin-top: var(--space-3);
  font-family: var(--font-display);
  font-size: clamp(25px, 3vw, 36px);
}

.identity-card__copy p {
  max-width: 360px;
  margin: var(--space-3) 0 0;
  color: rgba(255, 255, 255, 0.7);
  font-size: 13px;
  line-height: 1.7;
}

.metric-strip {
  position: relative;
  z-index: 1;
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(112px, 1fr));
  align-items: stretch;
  border-left: 1px solid rgba(255, 255, 255, 0.12);
}

.metric-strip__item {
  display: flex;
  min-height: 154px;
  flex-direction: column;
  justify-content: flex-end;
  padding: var(--space-5);
  border-right: 1px solid rgba(255, 255, 255, 0.1);
}

.metric-strip__item span {
  color: rgba(255, 255, 255, 0.58);
  font-size: 11px;
}

.metric-strip__item strong {
  margin-top: var(--space-2);
  color: #fffdf8;
  font-family: var(--font-display);
  font-size: 36px;
  font-variant-numeric: tabular-nums;
}

.metric-strip__item small {
  margin-left: 4px;
  font-family: var(--font-body);
  font-size: 11px;
  font-weight: 600;
}

.dashboard-section,
.activity-board {
  margin-top: 52px;
}

.section-heading,
.activity-board > header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-5);
  margin-bottom: var(--space-5);
}

.section-heading h2,
.activity-board h2 {
  margin: 6px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 25px;
}

.section-heading > div > span,
.activity-board header > div > span {
  color: var(--color-brand-strong);
}

.section-heading > small {
  color: var(--color-text-soft);
}

.module-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-4);
}

.module-grid--admin {
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.module-card {
  display: grid;
  min-height: 245px;
  grid-template-rows: auto 1fr auto;
  gap: var(--space-5);
  padding: var(--space-5);
  border: 1px solid var(--color-line);
  border-radius: var(--radius-md);
  background: var(--color-surface);
  color: inherit;
  text-decoration: none;
  box-shadow: 0 10px 28px rgba(31, 54, 52, 0.045);
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease,
    transform 0.2s ease;
}

.module-card:hover,
.module-card:focus-visible {
  border-color: var(--color-brand-border);
  outline: none;
  box-shadow: 0 18px 42px rgba(31, 54, 52, 0.11);
  transform: translateY(-4px);
}

.module-card__index {
  color: var(--color-accent);
  font-family: var(--font-mono);
  font-size: 11px;
}

.module-card h3 {
  margin: 0 0 var(--space-3);
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 22px;
}

.module-card p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.75;
}

.module-card footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding-top: var(--space-4);
  border-top: 1px solid var(--color-line);
  color: var(--color-text-muted);
  font-size: 11px;
}

.module-card footer strong {
  color: var(--color-brand-strong);
  font-size: 18px;
}

.activity-board {
  padding: var(--space-6);
  border: 1px solid var(--color-line);
  border-radius: var(--radius-lg);
  background: rgba(255, 253, 248, 0.7);
}

.activity-board header a {
  color: var(--color-brand-strong);
  font-size: 12px;
  font-weight: 800;
  text-decoration: none;
}

.activity-list {
  display: grid;
}

.activity-item {
  display: grid;
  grid-template-columns: 4px minmax(0, 1fr) auto 126px;
  align-items: center;
  gap: var(--space-4);
  min-height: 78px;
  border-top: 1px solid var(--color-line);
}

.activity-item__line {
  width: 3px;
  height: 28px;
  border-radius: 99px;
  background: var(--color-brand-border);
}

.activity-item strong {
  color: var(--color-ink);
  font-size: 13px;
}

.activity-item p {
  margin: 4px 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.activity-item time {
  color: var(--color-text-soft);
  font-family: var(--font-mono);
  font-size: 10px;
  text-align: right;
}

.activity-empty {
  display: grid;
  min-height: 160px;
  place-items: center;
  border-top: 1px solid var(--color-line);
  color: var(--color-text-muted);
}

@media (max-width: 960px) {
  .identity-card {
    grid-template-columns: 1fr;
  }

  .metric-strip {
    border-top: 1px solid rgba(255, 255, 255, 0.12);
    border-left: 0;
  }

  .metric-strip__item {
    min-height: 120px;
  }

  .module-grid,
  .module-grid--admin {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 680px) {
  .dashboard-page {
    width: min(100% - 24px, var(--content-max));
  }

  .module-grid,
  .module-grid--admin {
    grid-template-columns: 1fr;
  }

  .module-card {
    min-height: 210px;
  }

  .activity-item {
    grid-template-columns: 4px minmax(0, 1fr) auto;
  }

  .activity-item time {
    display: none;
  }

  .section-heading > small {
    display: none;
  }
}
</style>
