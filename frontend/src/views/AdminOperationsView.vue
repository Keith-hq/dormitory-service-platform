<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { adminApi } from '@/api/admin'
import { PageHeader, RecordsTable, StatusTag } from '@/components'
import { normalizeCollection } from '@/utils/collection'

const route = useRoute()
const router = useRouter()

const tabs = [
  { key: 'water', label: '配送队列' },
  { key: 'access', label: '门禁记录' },
  { key: 'violations', label: '违规台账' }
]
const validTabs = new Set(tabs.map((tab) => tab.key))
const activeTab = ref(validTabs.has(route.query.tab) ? route.query.tab : 'water')
const loading = ref(true)
const actionLoading = ref('')
const feedback = ref('')
const waterOrders = ref([])
const accessLogs = ref([])
const violations = ref([])

const statusLabel = {
  pending: '待处理',
  delivering: '配送中',
  completed: '已送达',
  normal: '正常',
  late: '晚归异常',
  processed: '已处理'
}

const statusTone = (status) => {
  if (['completed', 'normal', 'processed'].includes(status)) return 'success'
  if (status === 'late') return 'danger'
  if (status === 'delivering') return 'info'
  return 'warning'
}

const columns = computed(() => {
  if (activeTab.value === 'access') {
    return [
      { key: 'studentName', label: '学生' },
      { key: 'roomName', label: '房间' },
      { key: 'direction', label: '方向' },
      { key: 'accessTime', label: '通行时间' },
      { key: 'status', label: '判定' }
    ]
  }
  if (activeTab.value === 'violations') {
    return [
      { key: 'violationId', label: '记录号' },
      { key: 'studentName', label: '学生' },
      { key: 'roomName', label: '房间' },
      { key: 'type', label: '违规类型' },
      { key: 'scoreDelta', label: '信用分' },
      { key: 'status', label: '状态' },
      { key: 'occurredAt', label: '发生时间' }
    ]
  }
  return [
    { key: 'orderId', label: '订单号' },
    { key: 'studentName', label: '学生' },
    { key: 'roomName', label: '配送位置' },
    { key: 'quantity', label: '数量' },
    { key: 'amount', label: '金额' },
    { key: 'status', label: '状态' },
    { key: 'createdAt', label: '下单时间' }
  ]
})

const currentItems = computed(() => {
  if (activeTab.value === 'access') return accessLogs.value
  if (activeTab.value === 'violations') return violations.value
  return waterOrders.value
})

const currentTitle = computed(() => tabs.find((tab) => tab.key === activeTab.value)?.label || '')
const urgentCount = computed(
  () =>
    waterOrders.value.filter((item) => item.status === 'pending').length +
    accessLogs.value.filter((item) => item.status === 'late').length +
    violations.value.filter((item) => item.status === 'pending').length
)

const readItems = (payload) => normalizeCollection(payload).items

const setActiveTab = async (tab) => {
  if (tab === activeTab.value) return
  activeTab.value = tab
  feedback.value = ''
  await router.replace({ query: { ...route.query, tab } })
}

const loadAll = async () => {
  loading.value = true
  const results = await Promise.allSettled([
    adminApi.getWaterOrders(),
    adminApi.getAccessLogs(),
    adminApi.getViolations()
  ])
  const targets = [waterOrders, accessLogs, violations]
  const failed = []
  results.forEach((result, index) => {
    if (result.status === 'fulfilled') targets[index].value = readItems(result.value)
    else failed.push(tabs[index].label)
  })
  if (failed.length) feedback.value = `${failed.join('、')}暂时无法同步`
  loading.value = false
}

const reloadWaterOrders = async () => {
  waterOrders.value = readItems(await adminApi.getWaterOrders())
}

const updateWaterOrder = async (item) => {
  const isPending = item.status === 'pending'
  actionLoading.value = `order-${item.orderId}`
  feedback.value = ''
  try {
    if (isPending) await adminApi.markWaterOrderDelivering(item.orderId)
    else await adminApi.confirmWaterOrderDelivered(item.orderId)
    await reloadWaterOrders()
    feedback.value = isPending
      ? `订单 #${item.orderId} 已进入配送`
      : `订单 #${item.orderId} 已确认送达`
  } catch (error) {
    feedback.value = error.message || '订单状态更新失败'
  } finally {
    actionLoading.value = ''
  }
}

watch(
  () => route.query.tab,
  (tab) => {
    if (validTabs.has(tab)) activeTab.value = tab
  }
)

onMounted(loadAll)
</script>

<template>
  <div class="operations-page">
    <PageHeader
      eyebrow="DORM MANAGER DESK"
      title="值班工作台"
      description="将订水配送、门禁巡查和违规台账按处理顺序汇总，操作直接对应 Apifox 宿管端接口。"
    >
      <div class="urgent-counter" aria-label="当前待办总数">
        <span>今日待办</span>
        <strong>{{ loading ? '—' : urgentCount }}</strong>
      </div>
    </PageHeader>

    <section class="shift-note" aria-label="值班提示">
      <span class="shift-note__mark" aria-hidden="true">值</span>
      <div>
        <strong>桂苑 A 栋 · 白班</strong>
        <p>先处理晚归异常，再按下单顺序推进配送。所有状态变更均由后端接口校验。</p>
      </div>
      <StatusTag label="08:00–16:00" tone="info" />
    </section>

    <nav class="operation-tabs" aria-label="值班业务分类">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        type="button"
        :class="{ active: activeTab === tab.key }"
        :aria-current="activeTab === tab.key ? 'page' : undefined"
        @click="setActiveTab(tab.key)"
      >
        {{ tab.label }}
        <span>{{
          tab.key === 'water'
            ? waterOrders.length
            : tab.key === 'access'
              ? accessLogs.length
              : violations.length
        }}</span>
      </button>
    </nav>

    <p v-if="feedback" class="operation-feedback" role="status" aria-live="polite">
      {{ feedback }}
    </p>

    <RecordsTable
      eyebrow="OPERATION REGISTER"
      :title="currentTitle"
      :columns="columns"
      :items="currentItems"
      :loading="loading"
      :row-key="
        activeTab === 'water' ? 'orderId' : activeTab === 'access' ? 'logId' : 'violationId'
      "
    >
      <template #header>
        <router-link class="btn btn-sm" to="/building">楼栋档案</router-link>
      </template>
      <template #cell-quantity="{ value }">{{ value }} 桶</template>
      <template #cell-amount="{ value }">¥{{ value }}</template>
      <template #cell-scoreDelta="{ value }">
        <strong class="score-negative">{{ value }}</strong>
      </template>
      <template #cell-status="{ value }">
        <StatusTag :label="statusLabel[value] || value" :tone="statusTone(value)" size="small" />
      </template>
      <template v-if="activeTab === 'water'" #actions="{ item }">
        <button
          type="button"
          class="btn btn-sm"
          :class="{ 'btn-primary': item.status !== 'completed' }"
          :disabled="item.status === 'completed' || actionLoading === `order-${item.orderId}`"
          @click="updateWaterOrder(item)"
        >
          {{
            item.status === 'pending'
              ? '开始配送'
              : item.status === 'delivering'
                ? '确认送达'
                : '已完成'
          }}
        </button>
      </template>
    </RecordsTable>
  </div>
</template>

<style scoped>
.operations-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}

.urgent-counter {
  display: flex;
  align-items: baseline;
  gap: var(--space-3);
  padding: 12px 16px;
  border: 1px solid var(--color-line);
  border-radius: var(--radius-md);
  background: var(--color-surface);
}

.urgent-counter span {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 700;
}

.urgent-counter strong {
  color: var(--color-danger);
  font-family: var(--font-display);
  font-size: 28px;
}

.shift-note {
  display: flex;
  align-items: center;
  gap: var(--space-4);
  margin-top: var(--space-7);
  padding: var(--space-4) var(--space-5);
  border: 1px solid var(--color-brand-border);
  border-radius: 2px var(--radius-lg) 2px var(--radius-lg);
  background: var(--color-brand-soft);
}

.shift-note__mark {
  display: grid;
  width: 42px;
  height: 42px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 12px 3px 12px 3px;
  background: var(--color-brand-strong);
  color: #fff;
  font-family: var(--font-display);
  font-weight: 700;
}

.shift-note > div {
  flex: 1;
}

.shift-note strong {
  color: var(--color-ink);
  font-size: 13px;
}

.shift-note p {
  margin: 4px 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
}

.operation-tabs {
  display: flex;
  width: fit-content;
  gap: var(--space-2);
  margin: var(--space-5) 0;
  padding: 6px;
  border: 1px solid var(--color-line);
  border-radius: var(--radius-md);
  background: rgba(255, 253, 248, 0.78);
}

.operation-tabs button {
  display: inline-flex;
  align-items: center;
  min-height: 38px;
  gap: var(--space-2);
  border: 0;
  border-radius: var(--radius-sm);
  padding: 7px 14px;
  background: transparent;
  color: var(--color-text-muted);
  font: inherit;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
}

.operation-tabs button span {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border-radius: 99px;
  background: var(--color-canvas-deep);
  font-family: var(--font-mono);
  font-size: 9px;
}

.operation-tabs button.active {
  background: var(--color-ink);
  color: #fff;
}

.operation-tabs button.active span {
  background: rgba(255, 255, 255, 0.14);
}

.operation-tabs button:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 2px;
}

.operation-feedback {
  margin: 0 0 var(--space-4);
  padding: 11px 14px;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-sm);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 12px;
  font-weight: 700;
}

.score-negative {
  color: var(--color-danger);
}

:deep(.records-panel__header a) {
  text-decoration: none;
}

@media (max-width: 720px) {
  .operations-page {
    width: min(100% - 24px, var(--content-max));
  }

  .shift-note {
    align-items: flex-start;
  }

  .shift-note > .status-tag {
    display: none;
  }

  .operation-tabs {
    width: 100%;
    overflow-x: auto;
  }

  .operation-tabs button {
    white-space: nowrap;
  }
}
</style>
