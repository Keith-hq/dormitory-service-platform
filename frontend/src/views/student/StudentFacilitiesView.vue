<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const activeMode = ref('booking')
const loading = ref(true)
const error = ref('')
const facilities = ref([])
const sharedItems = ref([])
const loans = ref([])
const selectedResource = ref(null)
const selectedDate = ref('明天')
const selectedTime = ref('')
const feedback = ref('')
const actionLoading = ref('')
const DATE_OPTIONS = ['今天', '明天', '周日']
const TIME_SLOTS = ['08:00', '10:00', '14:00', '16:00', '19:00', '21:00']
const isOccupiedSlot = (time) => ['10:00', '19:00'].includes(time)
const studentId = computed(() => userStore.userInfo?.id || '')

const loadResources = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    studentApi.getFacilities({ page: 1, pageSize: 50 }),
    studentApi.getSharedItems({ page: 1, pageSize: 50 }),
    studentApi.getItemLoans(studentId.value)
  ])
  if (results[0].status === 'fulfilled')
    facilities.value = normalizeCollection(results[0].value).items
  if (results[1].status === 'fulfilled')
    sharedItems.value = normalizeCollection(results[1].value).items
  if (results[2].status === 'fulfilled') loans.value = normalizeCollection(results[2].value).items
  if (results.every((result) => result.status === 'rejected'))
    error.value = '设施与共享物品暂时无法同步'
  loading.value = false
}
const currentResources = computed(() =>
  activeMode.value === 'booking' ? facilities.value : sharedItems.value
)
const resourceName = (item) =>
  item.facilityName ||
  item.itemName ||
  item.name ||
  `资源 #${item.facilityId || item.itemId || item.id}`
const availableQuantity = (item) => Number(item.availableQty ?? item.quantity ?? item.stock ?? 0)
const resourceStatus = (item) => item.status || (availableQuantity(item) > 0 ? '可用' : '暂无库存')
const activeLoans = computed(() => loans.value.filter((loan) => !loan.returnTime))
const switchMode = (mode) => {
  activeMode.value = mode
  selectedResource.value = null
  feedback.value = ''
}
const createIdempotencyKey = () =>
  globalThis.crypto?.randomUUID?.() ||
  `loan-${Date.now()}-${Math.random().toString(36).slice(2, 10)}`
const submitBooking = async () => {
  if (!selectedResource.value?.facilityId) return
  actionLoading.value = 'booking'
  feedback.value = ''
  try {
    await studentApi.createFacilityBooking(selectedResource.value.facilityId)
    feedback.value = '预约成功，可在设施列表查看占用状态'
    await loadResources()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '预约失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}
const borrowSelected = async () => {
  if (!selectedResource.value?.itemId || availableQuantity(selectedResource.value) < 1) return
  actionLoading.value = 'borrow'
  feedback.value = ''
  try {
    await studentApi.createItemLoan(selectedResource.value.itemId, createIdempotencyKey())
    feedback.value = '借用成功，库存与借用记录已同步'
    await loadResources()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '借用失败，请确认库存和信用状态')
  } finally {
    actionLoading.value = ''
  }
}
const returnLoan = async (loan) => {
  actionLoading.value = `return-${loan.loanId}`
  feedback.value = ''
  try {
    await studentApi.returnItemLoan(loan.loanId)
    feedback.value = '归还成功，库存已释放'
    await loadResources()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '归还失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}
onMounted(loadResources)
</script>

<template>
  <div class="facilities-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / SHARED LIFE"
      title="设施预约与共享物品"
      description="预约围绕时间展开，借用围绕库存展开；两种服务使用不同操作路径。"
    >
      <StatusTag :label="`${loans.length} 项借用记录`" tone="info" />
    </WorkspaceHeader>
    <nav class="mode-switch" aria-label="服务类型">
      <button :class="{ active: activeMode === 'booking' }" @click="switchMode('booking')">
        设施预约 <span>{{ facilities.length }}</span></button
      ><button :class="{ active: activeMode === 'items' }" @click="switchMode('items')">
        共享物品 <span>{{ sharedItems.length }}</span>
      </button>
    </nav>
    <InlineState :loading="loading" :error="error" />

    <div v-if="!loading" class="resource-layout">
      <section class="resource-browser">
        <header>
          <div>
            <span>{{
              activeMode === 'booking' ? 'FACILITIES / SCHEDULE' : 'SHARED / INVENTORY'
            }}</span>
            <h2>{{ activeMode === 'booking' ? '选择可预约设施' : '查看物品库存' }}</h2>
          </div>
          <button class="btn btn-sm" @click="loadResources">刷新资源</button>
        </header>
        <InlineState
          :empty="!currentResources.length"
          :empty-text="activeMode === 'booking' ? '暂无开放设施' : '暂无共享物品'"
        />
        <div class="resource-grid">
          <button
            v-for="(item, index) in currentResources"
            :key="item.facilityId || item.itemId || index"
            :class="{ active: selectedResource === item }"
            @click="selectedResource = item"
          >
            <span>0{{ index + 1 }}</span>
            <h3>{{ resourceName(item) }}</h3>
            <p>{{ item.location || item.description || '服务位置与说明待同步' }}</p>
            <footer>
              <small>{{
                activeMode === 'booking'
                  ? '查看时段'
                  : `可借 ${availableQuantity(item)} / 共 ${item.totalQty ?? '—'}`
              }}</small
              ><StatusTag :label="resourceStatus(item)" tone="success" size="small" />
            </footer>
          </button>
        </div>
      </section>

      <aside
        class="schedule-panel"
        :class="{ 'schedule-panel--inventory': activeMode === 'items' }"
      >
        <header>
          <span>{{ activeMode === 'booking' ? 'TIME SLOTS' : 'BORROW FLOW' }}</span>
          <h2>{{ selectedResource ? resourceName(selectedResource) : '选择一项资源' }}</h2>
        </header>
        <template v-if="selectedResource && activeMode === 'booking'"
          ><p class="schedule-note">
            示例交互：当前预约按设施即时占位，日期/时段仅供展示，不随请求提交
          </p>
          <div class="date-line">
            <button
              v-for="day in DATE_OPTIONS"
              :key="day"
              :class="{ active: selectedDate === day }"
              @click="selectedDate = day"
            >
              {{ day }}
            </button>
          </div>
          <div class="time-slots">
            <button
              v-for="time in TIME_SLOTS"
              :key="time"
              :disabled="isOccupiedSlot(time)"
              :class="{ active: selectedTime === time }"
              @click="selectedTime = time"
            >
              {{ time }}<small>{{ isOccupiedSlot(time) ? '已占用' : '可预约' }}</small>
            </button>
          </div>
          <p v-if="feedback" class="schedule-feedback" role="status">{{ feedback }}</p>
          <button
            class="btn btn-primary schedule-action"
            :disabled="actionLoading === 'booking'"
            @click="submitBooking"
          >
            {{ actionLoading === 'booking' ? '预约中…' : '确认预约' }}
          </button></template
        >
        <template v-else-if="selectedResource"
          ><dl>
            <div>
              <dt>当前库存</dt>
              <dd>
                {{ availableQuantity(selectedResource) }} / {{ selectedResource.totalQty ?? '—' }}
              </dd>
            </div>
            <div>
              <dt>信用要求</dt>
              <dd>信用状态正常</dd>
            </div>
            <div>
              <dt>借用期限</dt>
              <dd>以物品规则为准</dd>
            </div>
          </dl>
          <p v-if="feedback" class="schedule-feedback" role="status">{{ feedback }}</p>
          <button
            class="btn btn-primary schedule-action"
            :disabled="actionLoading === 'borrow' || availableQuantity(selectedResource) < 1"
            @click="borrowSelected"
          >
            {{
              actionLoading === 'borrow'
                ? '借用中…'
                : availableQuantity(selectedResource) < 1
                  ? '暂无库存'
                  : '申请借用'
            }}
          </button>
          <section class="loan-ledger">
            <header>
              <span>ACTIVE LOANS</span><strong>我的待归还 {{ activeLoans.length }}</strong>
            </header>
            <article v-for="loan in activeLoans" :key="loan.loanId">
              <div>
                <b>物品 #{{ loan.itemId }}</b
                ><small
                  >借用单 #{{ loan.loanId }} ·
                  {{
                    loan.dueTime
                      ? `应还 ${new Date(loan.dueTime).toLocaleDateString()}`
                      : '归还期限待同步'
                  }}</small
                >
              </div>
              <button
                class="btn btn-sm"
                :disabled="actionLoading === `return-${loan.loanId}`"
                @click="returnLoan(loan)"
              >
                {{ actionLoading === `return-${loan.loanId}` ? '归还中…' : '确认归还' }}
              </button>
            </article>
            <p v-if="!activeLoans.length">当前没有待归还物品。</p>
          </section></template
        >
        <InlineState v-else empty empty-text="从左侧选择设施或物品" />
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
.mode-switch {
  display: flex;
  margin: 27px 0 15px;
  border-bottom: 1px solid var(--color-line-strong);
}
.mode-switch button {
  display: flex;
  align-items: center;
  min-height: 45px;
  padding: 0 20px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: var(--color-text-muted);
  font-size: 11px;
  gap: 9px;
  cursor: pointer;
}
.mode-switch button span {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border: 1px solid var(--color-line-strong);
  font: 8px var(--font-mono);
}
.mode-switch button.active {
  border-color: var(--color-accent);
  color: var(--color-ink);
  font-weight: 600;
}
.resource-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(300px, 0.62fr);
  gap: 16px;
}
.resource-browser,
.schedule-panel {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.52);
}
.resource-browser > header,
.schedule-panel > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 72px;
  padding: 16px 19px;
  border-bottom: 1px solid var(--color-line);
}
.resource-browser header span,
.schedule-panel header span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.resource-browser h2,
.schedule-panel h2 {
  margin: 6px 0 0;
  font-family: var(--font-display);
  font-size: 19px;
  font-weight: 500;
}
.resource-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
}
.resource-grid > button {
  display: grid;
  min-height: 160px;
  padding: 18px;
  border: 0;
  border-right: 1px solid var(--color-line);
  border-bottom: 1px solid var(--color-line);
  background: transparent;
  text-align: left;
  cursor: pointer;
}
.resource-grid > button:hover,
.resource-grid > button.active {
  background: var(--color-brand-soft);
}
.resource-grid > button.active {
  box-shadow: inset 3px 0 var(--color-accent);
}
.resource-grid > button > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.resource-grid h3 {
  align-self: end;
  margin: 20px 0 4px;
  font-family: var(--font-display);
  font-size: 16px;
}
.resource-grid p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 9px;
}
.resource-grid footer {
  display: flex;
  align-items: end;
  justify-content: space-between;
  margin-top: 14px;
}
.resource-grid footer small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.schedule-panel {
  background: var(--color-ink);
  color: var(--color-paper);
}
.schedule-panel > header {
  border-color: #405249;
}
.schedule-note {
  margin: 14px 18px 0;
  color: #83968a;
  font-size: 9px;
  line-height: 1.6;
}
.date-line {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  padding: 18px 18px 0;
  gap: 7px;
}
.date-line button {
  padding: 8px;
  border: 1px solid #4e6257;
  background: transparent;
  color: #8fa096;
  font-size: 9px;
}
.date-line button.active {
  border-color: #d48769;
  color: #f4edde;
}
.time-slots {
  display: grid;
  grid-template-columns: 1fr 1fr;
  padding: 13px 18px;
  gap: 7px;
}
.time-slots button {
  display: grid;
  padding: 12px;
  border: 1px solid #4d6156;
  background: #203329;
  color: #f3ebdd;
  font: 10px var(--font-mono);
  text-align: left;
  gap: 5px;
  cursor: pointer;
}
.time-slots button small {
  color: #799084;
  font: 8px var(--font-body);
}
.time-slots button:disabled {
  opacity: 0.4;
}
.time-slots button.active {
  border-color: var(--color-accent);
  background: #2c4438;
}
.schedule-feedback {
  margin: 13px 18px 0;
  padding: 10px 12px;
  border-left: 3px solid #d48769;
  background: #2a3a31;
  color: #f4edde;
  font-size: 10px;
}
.schedule-action {
  width: calc(100% - 36px);
  margin: 8px 18px;
}
.schedule-panel dl {
  margin: 20px 18px;
}
.schedule-panel dl div {
  display: flex;
  justify-content: space-between;
  padding: 15px 0;
  border-bottom: 1px solid #41564a;
}
.schedule-panel dt {
  color: #83968a;
  font-size: 9px;
}
.schedule-panel dd {
  margin: 0;
  font-family: var(--font-display);
  font-size: 12px;
}
.loan-ledger {
  margin: 24px 18px 18px;
  border-top: 1px solid #41564a;
}
.loan-ledger > header,
.loan-ledger article {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  padding: 14px 0;
  border-bottom: 1px solid #41564a;
}
.loan-ledger header span,
.loan-ledger small {
  display: block;
  color: #83968a;
  font: 8px/1.5 var(--font-mono);
}
.loan-ledger header strong,
.loan-ledger b {
  font-size: 10px;
  font-weight: 500;
}
.loan-ledger p {
  color: #83968a;
  font-size: 9px;
}
@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .resource-layout {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 560px) {
  .resource-grid {
    grid-template-columns: 1fr;
  }
}
</style>
