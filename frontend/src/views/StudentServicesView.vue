<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { studentApi } from '@/api/student'
import { PageHeader, RecordsTable, StatusTag } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

const route = useRoute()
const router = useRouter()
const userStore = useUserStore()

const tabs = [
  { key: 'water', label: '订水记录' },
  { key: 'packages', label: '我的快递' },
  { key: 'appeals', label: '信用申诉' }
]
const validTabs = new Set(tabs.map((tab) => tab.key))
const activeTab = ref(validTabs.has(route.query.tab) ? route.query.tab : 'water')
const loading = ref(true)
const actionLoading = ref('')
const feedback = ref('')
const orderQuantity = ref(1)
const waterOrders = ref([])
const packages = ref([])
const appeals = ref([])

const statusLabel = {
  pending: '待配送',
  delivering: '配送中',
  completed: '已送达',
  ready: '待取件',
  picked_up: '已取件',
  reviewing: '复核中',
  approved: '已通过',
  rejected: '未通过'
}

const statusTone = (status) => {
  if (['completed', 'picked_up', 'approved'].includes(status)) return 'success'
  if (['delivering', 'reviewing'].includes(status)) return 'info'
  if (status === 'rejected') return 'danger'
  return 'warning'
}

const columns = computed(() => {
  if (activeTab.value === 'packages') {
    return [
      { key: 'courierCompany', label: '快递公司' },
      { key: 'trackingNo', label: '运单号' },
      { key: 'shelfCode', label: '货架位置' },
      { key: 'pickupCode', label: '取件码' },
      { key: 'status', label: '状态' },
      { key: 'arrivedAt', label: '到站时间' }
    ]
  }
  if (activeTab.value === 'appeals') {
    return [
      { key: 'appealId', label: '申诉编号' },
      { key: 'reason', label: '申诉原因' },
      { key: 'scoreChange', label: '分值变动' },
      { key: 'status', label: '状态' },
      { key: 'createdAt', label: '提交时间' }
    ]
  }
  return [
    { key: 'orderId', label: '订单号' },
    { key: 'quantity', label: '数量' },
    { key: 'amount', label: '金额' },
    { key: 'status', label: '状态' },
    { key: 'createdAt', label: '下单时间' }
  ]
})

const currentItems = computed(() => {
  if (activeTab.value === 'packages') return packages.value
  if (activeTab.value === 'appeals') return appeals.value
  return waterOrders.value
})

const currentTitle = computed(() => tabs.find((tab) => tab.key === activeTab.value)?.label || '')

const setActiveTab = async (tab) => {
  if (tab === activeTab.value) return
  activeTab.value = tab
  feedback.value = ''
  await router.replace({ query: { ...route.query, tab } })
}

const readItems = (payload) => normalizeCollection(payload).items

const loadAll = async () => {
  loading.value = true
  const studentId = userStore.userInfo.id
  const results = await Promise.allSettled([
    studentApi.getWaterOrders(studentId),
    studentApi.getPackages(studentId),
    studentApi.getCreditAppeals(studentId)
  ])
  const targets = [waterOrders, packages, appeals]
  const failed = []
  results.forEach((result, index) => {
    if (result.status === 'fulfilled') targets[index].value = readItems(result.value)
    else failed.push(tabs[index].label)
  })
  if (failed.length) feedback.value = `${failed.join('、')}暂时无法同步`
  loading.value = false
}

const reloadWaterOrders = async () => {
  waterOrders.value = readItems(await studentApi.getWaterOrders(userStore.userInfo.id))
}

const reloadPackages = async () => {
  packages.value = readItems(await studentApi.getPackages(userStore.userInfo.id))
}

const createWaterOrder = async () => {
  actionLoading.value = 'create-water'
  feedback.value = ''
  try {
    await studentApi.createWaterOrder({ quantity: orderQuantity.value })
    await reloadWaterOrders()
    feedback.value = `已提交 ${orderQuantity.value} 桶饮用水订单`
    orderQuantity.value = 1
  } catch (error) {
    feedback.value = error.message || '订水失败，请稍后重试'
  } finally {
    actionLoading.value = ''
  }
}

const pickupPackage = async (item) => {
  actionLoading.value = `package-${item.packageId}`
  feedback.value = ''
  try {
    await studentApi.pickupPackage(item.packageId)
    await reloadPackages()
    feedback.value = `${item.courierCompany}快递已确认取件`
  } catch (error) {
    feedback.value = error.message || '确认取件失败'
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
  <div class="service-page">
    <PageHeader
      eyebrow="STUDENT SERVICES"
      title="我的生活服务"
      description="对照 Apifox 学生端契约，集中查看订水、快递与信用申诉。Mock 操作会即时回写当前页面状态。"
    >
      <StatusTag label="学生视图" tone="info" />
    </PageHeader>

    <nav class="service-tabs" aria-label="生活服务分类">
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
            : tab.key === 'packages'
              ? packages.length
              : appeals.length
        }}</span>
      </button>
    </nav>

    <section v-if="activeTab === 'water'" class="order-station" aria-labelledby="order-title">
      <div>
        <span>QUICK ORDER</span>
        <h2 id="order-title">宿舍订水</h2>
        <p>{{ userStore.userInfo.buildingName }} · {{ userStore.userInfo.roomName }}，每桶 ¥8</p>
      </div>
      <form @submit.prevent="createWaterOrder">
        <label for="water-quantity">订购数量</label>
        <select id="water-quantity" v-model.number="orderQuantity" class="form-select">
          <option v-for="quantity in 4" :key="quantity" :value="quantity">{{ quantity }} 桶</option>
        </select>
        <button class="btn btn-primary" type="submit" :disabled="actionLoading === 'create-water'">
          {{ actionLoading === 'create-water' ? '提交中…' : '提交订单' }}
        </button>
      </form>
    </section>

    <p v-if="feedback" class="service-feedback" role="status" aria-live="polite">
      {{ feedback }}
    </p>

    <RecordsTable
      eyebrow="CONTRACT RECORDS"
      :title="currentTitle"
      :columns="columns"
      :items="currentItems"
      :loading="loading"
      :row-key="
        activeTab === 'water' ? 'orderId' : activeTab === 'packages' ? 'packageId' : 'appealId'
      "
    >
      <template #cell-quantity="{ value }">{{ value }} 桶</template>
      <template #cell-amount="{ value }">¥{{ value }}</template>
      <template #cell-scoreChange="{ value }">
        <strong :class="value > 0 ? 'score-positive' : 'score-negative'">
          {{ value > 0 ? '+' : '' }}{{ value }}
        </strong>
      </template>
      <template #cell-status="{ value }">
        <StatusTag :label="statusLabel[value] || value" :tone="statusTone(value)" size="small" />
      </template>
      <template v-if="activeTab === 'packages'" #actions="{ item }">
        <button
          type="button"
          class="btn btn-sm btn-primary"
          :disabled="item.status !== 'ready' || actionLoading === `package-${item.packageId}`"
          @click="pickupPackage(item)"
        >
          {{ item.status === 'ready' ? '确认取件' : '已取件' }}
        </button>
      </template>
    </RecordsTable>
  </div>
</template>

<style scoped>
.service-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}

.service-tabs {
  display: flex;
  gap: var(--space-2);
  margin: var(--space-7) 0 var(--space-5);
  padding: 6px;
  border: 1px solid var(--color-line);
  border-radius: 999px;
  background: rgba(255, 253, 248, 0.78);
  width: fit-content;
}

.service-tabs button {
  display: inline-flex;
  align-items: center;
  min-height: 38px;
  gap: var(--space-2);
  border: 0;
  border-radius: 999px;
  padding: 7px 14px;
  background: transparent;
  color: var(--color-text-muted);
  font: inherit;
  font-size: 13px;
  font-weight: 700;
  cursor: pointer;
}

.service-tabs button span {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border-radius: 99px;
  background: var(--color-canvas-deep);
  font-family: var(--font-mono);
  font-size: 9px;
}

.service-tabs button.active {
  background: var(--color-ink);
  color: #fff;
}

.service-tabs button.active span {
  background: rgba(255, 255, 255, 0.14);
}

.service-tabs button:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 2px;
}

.order-station {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-6);
  margin-bottom: var(--space-5);
  padding: var(--space-5) var(--space-6);
  border-radius: 2px var(--radius-lg) 2px var(--radius-lg);
  background: linear-gradient(120deg, rgba(255, 255, 255, 0.07), transparent 36%), var(--color-ink);
  color: #fff;
}

.order-station > div > span {
  color: rgba(255, 255, 255, 0.52);
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.17em;
}

.order-station h2 {
  margin: 5px 0 3px;
  color: #fff;
  font-family: var(--font-display);
  font-size: 23px;
}

.order-station p {
  margin: 0;
  color: rgba(255, 255, 255, 0.64);
  font-size: 12px;
}

.order-station form {
  display: flex;
  align-items: center;
  gap: var(--space-3);
}

.order-station label {
  font-size: 12px;
  font-weight: 700;
}

.order-station .form-select {
  min-width: 108px;
}

.service-feedback {
  margin: 0 0 var(--space-4);
  padding: 11px 14px;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-sm);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 12px;
  font-weight: 700;
}

.score-positive {
  color: #246b4b;
}

.score-negative {
  color: var(--color-danger);
}

@media (max-width: 720px) {
  .service-page {
    width: min(100% - 24px, var(--content-max));
  }

  .service-tabs {
    width: 100%;
    overflow-x: auto;
    border-radius: var(--radius-md);
  }

  .service-tabs button {
    white-space: nowrap;
  }

  .order-station {
    align-items: stretch;
    flex-direction: column;
  }

  .order-station form {
    flex-wrap: wrap;
  }
}
</style>
