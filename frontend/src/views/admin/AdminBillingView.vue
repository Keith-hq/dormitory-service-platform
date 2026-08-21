<script setup>
import { computed, onMounted, ref } from 'vue'
import { utilityApi } from '@/api/utility'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'
import { formatLocalMonthInput } from '@/utils/localDate'

const currentMonth = formatLocalMonthInput()
const loading = ref(true)
const working = ref('')
const error = ref('')
const feedback = ref('')
const bills = ref([])
const total = ref(0)
const page = ref(1)
const PAGE_SIZE = 20
const selected = ref(null)
const details = ref([])
const filters = ref({ buildingId: '', yearMonth: currentMonth, isPaid: '', publishStatus: '' })
const createForm = ref({ roomId: '', yearMonth: currentMonth, waterFee: '', elecFee: '' })
const editForm = ref({ waterFee: '', elecFee: '' })
const powerRoomId = ref('')
const powerStatus = ref(null)

const formatMoney = (value) =>
  value === null || value === undefined ? '—' : `¥ ${Number(value).toFixed(2)}`
const statusOf = (bill) => bill.publishStatus || '未发布'
const totalPages = computed(() => Math.max(1, Math.ceil(total.value / PAGE_SIZE)))
const metrics = computed(() => [
  { label: '筛选账单', value: total.value, hint: filters.value.yearMonth || '全部账期' },
  {
    label: '已发布',
    value: bills.value.filter((item) => /已发布|published/i.test(statusOf(item))).length,
    hint: '当前页'
  },
  {
    label: '已缴清',
    value: bills.value.filter((item) => /是|true|已缴/i.test(String(item.isPaid))).length,
    hint: '当前页'
  }
])

const load = async () => {
  loading.value = true
  error.value = ''
  try {
    const data = await utilityApi.getBills({
      buildingId: filters.value.buildingId || undefined,
      yearMonth: filters.value.yearMonth || undefined,
      isPaid: filters.value.isPaid === '' ? undefined : filters.value.isPaid === 'true',
      publishStatus: filters.value.publishStatus || undefined,
      page: page.value,
      pageSize: PAGE_SIZE
    })
    const normalized = normalizeCollection(data)
    bills.value = normalized.items
    total.value = normalized.total
    if (selected.value) {
      selected.value = bills.value.find((item) => item.feeId === selected.value.feeId) ?? null
    }
  } catch (e) {
    error.value = toUserMessage(e, '水电账单暂时无法同步')
  } finally {
    loading.value = false
  }
}

const applyFilters = () => {
  page.value = 1
  return load()
}

const goToPage = async (nextPage) => {
  if (nextPage < 1 || nextPage > totalPages.value || nextPage === page.value) return
  page.value = nextPage
  await load()
}

const run = async (key, action, success, reload = false) => {
  working.value = key
  feedback.value = ''
  try {
    const data = await action()
    feedback.value = success
    if (reload) await load()
    return data
  } catch (e) {
    feedback.value = toUserMessage(e, '操作失败，请核对账单状态后重试')
    return null
  } finally {
    working.value = ''
  }
}

const createBill = async () => {
  const data = await run(
    'create',
    () =>
      utilityApi.createBill({
        roomId: Number(createForm.value.roomId),
        yearMonth: createForm.value.yearMonth,
        waterFee: Number(createForm.value.waterFee),
        elecFee: Number(createForm.value.elecFee)
      }),
    '账单已录入',
    true
  )
  if (data) createForm.value = { roomId: '', yearMonth: currentMonth, waterFee: '', elecFee: '' }
}

const selectBill = (bill) => {
  selected.value = bill
  editForm.value = { waterFee: bill.waterFee ?? '', elecFee: bill.powerFee ?? bill.elecFee ?? '' }
  details.value = []
  feedback.value = ''
}

const updateBill = () =>
  run(
    'update',
    () =>
      utilityApi.updateBill(selected.value.feeId, {
        waterFee: Number(editForm.value.waterFee),
        elecFee: Number(editForm.value.elecFee)
      }),
    '账单金额已更新',
    true
  )

const publishBill = () =>
  run('publish', () => utilityApi.publishBill(selected.value.feeId), '账单已发布并触发分摊', true)
const allocateBill = () =>
  run('allocate', () => utilityApi.allocateBill(selected.value.feeId), '账单分摊已执行', true)
const loadDetails = async () => {
  const data = await run(
    'details',
    () => utilityApi.getBillDetails(selected.value.feeId),
    '分摊明细已刷新'
  )
  if (data) details.value = Array.isArray(data.items) ? data.items : []
}
const loadPowerStatus = async () => {
  const data = await run(
    'power',
    () => utilityApi.getPowerStatus(powerRoomId.value),
    '供电状态已刷新'
  )
  if (data) powerStatus.value = data
}

onMounted(load)
</script>

<template>
  <main class="billing-page">
    <WorkspaceHeader
      eyebrow="UTILITY LEDGER"
      title="水电账单"
      description="按账期集中录入、修改、发布与分摊房间水电费，并随时核对个人明细和供电状态。"
    >
      <button class="btn btn-sm" :disabled="loading" @click="load">刷新账本</button>
    </WorkspaceHeader>
    <MetricStrip :metrics="metrics" style="--metric-count: 3" />

    <form class="filter-bar" @submit.prevent="applyFilters">
      <label
        >楼栋 ID<input v-model="filters.buildingId" min="1" type="number" placeholder="全部"
      /></label>
      <label>账期<input v-model="filters.yearMonth" type="month" /></label>
      <label
        >缴费状态<select v-model="filters.isPaid">
          <option value="">全部</option>
          <option value="true">已缴</option>
          <option value="false">未缴</option>
        </select></label
      >
      <label
        >发布状态<select v-model="filters.publishStatus">
          <option value="">全部</option>
          <option>未发布</option>
          <option>已发布</option>
        </select></label
      >
      <button class="btn" type="submit">应用筛选</button>
    </form>
    <InlineState :loading="loading" :error="error" />
    <p v-if="feedback" class="feedback">{{ feedback }}</p>

    <section v-if="!loading" class="ledger-layout">
      <div class="ledger-main">
        <header>
          <div>
            <span>01 / MONTHLY LEDGER</span>
            <h2>账单列表</h2>
          </div>
          <b>{{ bills.length }} 笔当前页</b>
        </header>
        <div class="bill-table" role="table" aria-label="水电账单列表">
          <div class="bill-row bill-row--head" role="row">
            <span>账单 / 房间</span><span>账期</span><span>水费</span><span>电费</span
            ><span>状态</span>
          </div>
          <button
            v-for="bill in bills"
            :key="bill.feeId"
            class="bill-row"
            :class="{ active: selected?.feeId === bill.feeId }"
            type="button"
            @click="selectBill(bill)"
          >
            <span
              ><b>#{{ bill.feeId }}</b
              ><small>房间 {{ bill.roomId }}</small></span
            ><span>{{ bill.yearMonth }}</span
            ><span>{{ formatMoney(bill.waterFee) }}</span
            ><span>{{ formatMoney(bill.powerFee ?? bill.elecFee) }}</span
            ><span
              ><em>{{ statusOf(bill) }}</em
              ><small>{{ String(bill.isPaid) === '是' ? '已缴' : '待缴' }}</small></span
            >
          </button>
          <p v-if="!bills.length" class="empty">当前筛选条件下暂无账单。</p>
        </div>
        <nav class="pager" aria-label="账单分页">
          <button class="btn btn-sm" :disabled="page <= 1" @click="goToPage(page - 1)">
            上一页
          </button>
          <span>第 {{ page }} / {{ totalPages }} 页 · 共 {{ total }} 笔</span>
          <button class="btn btn-sm" :disabled="page >= totalPages" @click="goToPage(page + 1)">
            下一页
          </button>
        </nav>
      </div>

      <aside class="ledger-tools">
        <section class="create-panel">
          <header>
            <span>02 / NEW ENTRY</span>
            <h2>录入账单</h2>
          </header>
          <form @submit.prevent="createBill">
            <label
              >房间 ID<input v-model="createForm.roomId" required min="1" type="number"
            /></label>
            <label>账期<input v-model="createForm.yearMonth" required type="month" /></label>
            <label
              >水费<input v-model="createForm.waterFee" required min="0" step="0.01" type="number"
            /></label>
            <label
              >电费<input v-model="createForm.elecFee" required min="0" step="0.01" type="number"
            /></label>
            <button class="btn btn-primary" :disabled="working === 'create'">录入账本</button>
          </form>
        </section>
        <section class="power-panel">
          <header>
            <span>03 / POWER STATUS</span>
            <h2>供电核验</h2>
          </header>
          <form @submit.prevent="loadPowerStatus">
            <input
              v-model="powerRoomId"
              required
              min="1"
              type="number"
              placeholder="房间 ID"
            /><button class="btn btn-sm">查询</button>
          </form>
          <p v-if="powerStatus">
            <b>房间 {{ powerStatus.roomId }}</b
            ><span>{{ powerStatus.powerStatus }}</span>
          </p>
        </section>
      </aside>
    </section>

    <section v-if="selected" class="bill-sheet">
      <header>
        <div>
          <span>SELECTED BILL</span>
          <h2>#{{ selected.feeId }} · 房间 {{ selected.roomId }}</h2>
        </div>
        <b>{{ statusOf(selected) }}</b>
      </header>
      <div class="bill-actions">
        <form @submit.prevent="updateBill">
          <label
            >水费<input
              v-model="editForm.waterFee"
              required
              min="0"
              step="0.01"
              type="number" /></label
          ><label
            >电费<input
              v-model="editForm.elecFee"
              required
              min="0"
              step="0.01"
              type="number" /></label
          ><button class="btn" :disabled="working">保存金额</button>
        </form>
        <div>
          <button class="btn" :disabled="working" @click="publishBill">发布并分摊</button
          ><button class="btn" :disabled="working" @click="allocateBill">重新分摊</button
          ><button class="btn btn-primary" :disabled="working" @click="loadDetails">
            查看明细
          </button>
        </div>
      </div>
      <div v-if="details.length" class="detail-table">
        <div class="detail-row detail-row--head">
          <span>学生</span><span>在住天数</span><span>水费分摊</span><span>电费分摊</span
          ><span>应缴合计</span><span>状态</span>
        </div>
        <div v-for="item in details" :key="item.detailId" class="detail-row">
          <span>{{ item.studentId }}</span
          ><span>{{ item.stayDays }} / {{ item.totalDays }}</span
          ><span>{{ formatMoney(item.waterShare) }}</span
          ><span>{{ formatMoney(item.powerShare) }}</span
          ><span>{{ formatMoney(item.total) }}</span
          ><span>{{ item.isPaid }}</span>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.billing-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.filter-bar {
  display: grid;
  grid-template-columns: repeat(4, 1fr) auto;
  gap: 12px;
  align-items: end;
  margin-top: 22px;
  padding: 20px 22px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.filter-bar label,
.create-panel label,
.bill-actions label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  min-width: 0;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
}
.filter-bar input,
.filter-bar select,
.ledger-tools input,
.bill-sheet input {
  width: 100%;
  min-width: 0;
  max-width: 100%;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 14px;
}
.filter-bar input:focus-visible,
.filter-bar select:focus-visible,
.ledger-tools input:focus-visible,
.bill-sheet input:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}
.feedback {
  padding: 12px 16px;
  margin: 16px 0 0;
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  border-radius: var(--radius-lg);
  font-size: 13px;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: auto;
  padding: 14px 18px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
  color: var(--color-text-muted);
  font-size: 12px;
}
.ledger-layout {
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: 22px;
  margin-top: 28px;
}
.ledger-main,
.ledger-tools section,
.bill-sheet {
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.ledger-main {
  display: flex;
  flex-direction: column;
  overflow: hidden;
}
.ledger-main > header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding: 20px 22px 14px;
}
.ledger-main > header span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}
.ledger-main > header h2 {
  margin: 7px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 23px;
  font-weight: 900;
  line-height: 1.2;
  letter-spacing: 0;
}
.ledger-main > header b {
  flex: 0 0 auto;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}
.bill-table {
  display: grid;
  align-content: start;
  flex: 1;
  gap: 10px;
  padding: 0 12px 12px;
}
.bill-row {
  display: grid;
  width: 100%;
  grid-template-columns: 1.3fr 0.8fr 0.8fr 0.8fr 1fr;
  align-items: center;
  min-height: 70px;
  padding: 12px 16px;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  text-align: left;
  cursor: pointer;
}
.bill-row:not(.bill-row--head):hover,
.bill-row.active {
  background: #e8f3ff;
  color: var(--color-ink);
  box-shadow: 0 14px 28px rgba(11, 99, 199, 0.12);
}
.bill-row--head {
  min-height: 36px;
  padding: 0 16px 4px;
  background: transparent;
  color: var(--color-brand-strong);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
  cursor: default;
  box-shadow: none;
}
.bill-row--head span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.45;
  letter-spacing: 0;
}
.bill-row span {
  display: flex;
  flex-direction: column;
  gap: 3px;
  font-size: 13px;
}
.bill-row b {
  color: var(--color-ink);
  font: 800 12px var(--font-mono);
}
.bill-row small {
  color: var(--color-text-muted);
  font-size: 12px;
}
.bill-row em {
  color: var(--color-brand-strong);
  font-size: 12px;
  font-style: normal;
}
.empty {
  padding: 24px;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 15px;
  line-height: 1.7;
}
.ledger-tools {
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.ledger-tools section {
  padding: 22px;
}
.ledger-tools header span,
.bill-sheet header span {
  color: var(--color-brand-strong);
  font: 800 12px var(--font-mono);
  letter-spacing: 0.12em;
}
.ledger-tools h2,
.bill-sheet h2 {
  margin: 7px 0 0;
  color: var(--color-ink);
  font: 900 23px var(--font-display);
}
.create-panel form {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
  margin-top: 18px;
}
.create-panel form button {
  grid-column: 1/-1;
  width: 100%;
  min-width: 0;
}
.power-panel form {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 8px;
  margin-top: 17px;
}
.power-panel > p {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin: 16px 0 0;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  font-size: 13px;
}
.power-panel > p span {
  color: var(--color-brand-strong);
}
.bill-sheet {
  margin-top: 24px;
  padding: 26px;
}
.bill-sheet > header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 0 0 18px;
}
.bill-sheet > header > b {
  color: var(--color-brand-strong);
  font-size: 13px;
  font-weight: 800;
}
.bill-actions {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 20px;
  padding: 0 0 18px;
}
.bill-actions form {
  display: flex;
  align-items: end;
  gap: 10px;
}
.bill-actions > div {
  display: flex;
  align-items: end;
  gap: 8px;
}
.detail-table {
  overflow-x: auto;
  padding-top: 6px;
}
.detail-row {
  display: grid;
  grid-template-columns: 1.2fr repeat(5, 1fr);
  min-width: 720px;
  margin-top: 10px;
  padding: 13px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  font-size: 12px;
}
.detail-row--head {
  margin-top: 0;
  background: transparent;
  color: var(--color-brand-strong);
  font: 800 12px var(--font-mono);
}
@media (max-width: 980px) {
  .filter-bar {
    grid-template-columns: 1fr 1fr;
  }
  .ledger-layout {
    grid-template-columns: 1fr;
  }
  .ledger-tools {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
  .bill-actions {
    grid-template-columns: 1fr;
  }
  .bill-actions > div {
    align-items: start;
  }
}
@media (max-width: 650px) {
  .filter-bar,
  .ledger-tools {
    grid-template-columns: 1fr;
  }
  .bill-table {
    overflow-x: auto;
  }
  .bill-row {
    min-width: 650px;
  }
  .bill-actions form {
    align-items: stretch;
    flex-direction: column;
  }
  .bill-actions > div {
    flex-wrap: wrap;
  }
}
</style>
