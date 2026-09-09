<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'
import { genRequestId } from '@/utils/id'

const userStore = useUserStore()
const activeMode = ref('booking')
const loading = ref(true)
const error = ref('')
const facilities = ref([])
const sharedItems = ref([])
const loans = ref([])
const bookings = ref([])
const selectedResource = ref(null)
const localToday = () => {
  const d = new Date()
  const m = String(d.getMonth() + 1).padStart(2, '0')
  const day = String(d.getDate()).padStart(2, '0')
  return `${d.getFullYear()}-${m}-${day}`
}
const selectedDate = ref(localToday())
const selectedTime = ref('')
const feedback = ref('')
const actionLoading = ref('')
const TIME_SLOTS = ['08:00', '10:00', '14:00', '16:00', '19:00', '21:00']
const availability = ref([]) // [{ facilityId, occupiedSlots }]，按所选日期从真实预约读取
const occupiedFor = (facilityId) => {
  const hit = availability.value.find((a) => Number(a.facilityId) === Number(facilityId))
  return hit ? hit.occupiedSlots : []
}
const isSlotTaken = (facilityId, time) => occupiedFor(facilityId).includes(time)
const loadAvailability = async () => {
  if (!selectedDate.value) return
  try {
    const data = await studentApi.getFacilityAvailability(selectedDate.value)
    availability.value = Array.isArray(data?.items) ? data.items : Array.isArray(data) ? data : []
  } catch {
    availability.value = []
  }
}
const onDateChange = () => {
  selectedTime.value = ''
  loadAvailability()
}
const studentId = computed(() => userStore.userInfo?.id || '')

const loadResources = async () => {
  loading.value = true
  error.value = ''
  const selectedId = selectedResource.value?.facilityId ?? selectedResource.value?.itemId
  const results = await Promise.allSettled([
    studentApi.getFacilities({ page: 1, pageSize: 50 }),
    studentApi.getSharedItems({ page: 1, pageSize: 50 }),
    studentApi.getItemLoans(studentId.value, { page: 1, pageSize: 50 }),
    studentApi.getMyBookings()
  ])
  if (results[0].status === 'fulfilled')
    facilities.value = normalizeCollection(results[0].value).items
  if (results[1].status === 'fulfilled')
    sharedItems.value = normalizeCollection(results[1].value).items
  if (results[2].status === 'fulfilled') loans.value = normalizeCollection(results[2].value).items
  if (results[3].status === 'fulfilled')
    bookings.value = normalizeCollection(results[3].value).items
  if (selectedId) {
    selectedResource.value =
      (activeMode.value === 'booking' ? facilities.value : sharedItems.value).find(
        (item) => (item.facilityId ?? item.itemId) === selectedId
      ) ?? null
  }
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
const formatLoanDate = (value) => (value ? new Date(value).toLocaleDateString() : '—')
const isLoanOverdue = (loan) =>
  !loan.returnTime && loan.dueTime && new Date(loan.dueTime).getTime() < Date.now()
const loanStatus = (loan) =>
  loan.returnTime ? '已归还' : isLoanOverdue(loan) ? '已超期' : '待归还'
const loanTone = (loan) =>
  loan.returnTime ? 'success' : isLoanOverdue(loan) ? 'danger' : 'warning'
const switchMode = (mode) => {
  activeMode.value = mode
  selectedResource.value = null
  feedback.value = ''
}
const submitBooking = async () => {
  if (!selectedResource.value?.facilityId) return
  if (!selectedDate.value || !selectedTime.value) {
    feedback.value = '请先选择日期和时段'
    return
  }
  if (isSlotTaken(selectedResource.value.facilityId, selectedTime.value)) {
    feedback.value = '该时段已被预约，请改选其它时段'
    return
  }
  actionLoading.value = 'booking'
  feedback.value = ''
  try {
    await studentApi.createFacilityBooking({
      facilityId: selectedResource.value.facilityId,
      date: selectedDate.value,
      timeSlot: selectedTime.value
    })
    feedback.value = '预约成功，该时段已被你占用'
    selectedTime.value = ''
    await Promise.all([loadResources(), loadAvailability()])
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
    await studentApi.createItemLoan(selectedResource.value.itemId, genRequestId())
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
const activeBookingFor = (item) =>
  bookings.value.find(
    (booking) =>
      booking.facilityId === item.facilityId && ['已预约', '使用中'].includes(booking.status)
  )
const startUse = async (booking) => {
  actionLoading.value = `start-${booking.bookingId}`
  feedback.value = ''
  try {
    await studentApi.startFacilityUse(booking.bookingId)
    feedback.value = '已开始使用，请在结束后点击"结束使用"释放设施'
    await loadResources()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '开始使用失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}
const finishUse = async (booking) => {
  actionLoading.value = `finish-${booking.bookingId}`
  feedback.value = ''
  try {
    await studentApi.finishFacilityUse(booking.bookingId)
    feedback.value = '已结束使用，设施已释放'
    await loadResources()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '结束使用失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}
const bookingTone = (status) =>
  status === '已完成' ? 'success' : status === '已失效' ? 'neutral' : 'warning'
const formatTime = (value) => (value ? new Date(value).toLocaleString() : '—')
onMounted(() => {
  loadResources()
  loadAvailability()
})
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
    <p v-if="feedback" class="schedule-feedback global-feedback" role="status">
      {{ feedback }}
    </p>

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
              ><StatusTag
                :label="
                  activeMode === 'booking' && activeBookingFor(item)
                    ? activeBookingFor(item).status
                    : resourceStatus(item)
                "
                :tone="
                  activeMode === 'booking' && activeBookingFor(item)
                    ? bookingTone(activeBookingFor(item).status)
                    : 'success'
                "
                size="small"
              />
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
        <template v-if="activeMode === 'booking'"
          ><template v-if="selectedResource"
            ><div class="date-pick">
              <input
                v-model="selectedDate"
                type="date"
                :min="localToday()"
                @change="onDateChange"
              />
            </div>
            <div class="time-slots">
              <button
                v-for="time in TIME_SLOTS"
                :key="time"
                :disabled="isSlotTaken(selectedResource.facilityId, time)"
                :class="{
                  active: selectedTime === time,
                  taken: isSlotTaken(selectedResource.facilityId, time)
                }"
                @click="selectedTime = time"
              >
                {{ time }}<small>{{
                  isSlotTaken(selectedResource.facilityId, time) ? '已占用' : '可预约'
                }}</small>
              </button>
            </div>
            <button
              class="btn btn-primary schedule-action"
              :disabled="
                actionLoading === 'booking' ||
                !selectedTime ||
                isSlotTaken(selectedResource.facilityId, selectedTime) ||
                Boolean(activeBookingFor(selectedResource))
              "
              @click="submitBooking"
            >
              {{
                actionLoading === 'booking'
                  ? '预约中…'
                  : activeBookingFor(selectedResource)
                    ? '已有活跃预约'
                    : !selectedTime
                      ? '请先选择时段'
                      : isSlotTaken(selectedResource.facilityId, selectedTime)
                        ? '该时段已被约'
                        : '确认预约'
              }}
            </button></template
          ><InlineState v-else empty empty-text="从左侧选择设施或物品" />
          <section class="loan-ledger booking-ledger">
            <header>
              <span>MY BOOKINGS</span><strong>我的预约 {{ bookings.length }}</strong>
            </header>
            <article v-for="booking in bookings" :key="booking.bookingId">
              <div>
                <b>设施 #{{ booking.facilityId }}</b
                ><small
                  >预约单 #{{ booking.bookingId }} · {{ formatTime(booking.createTime) }}</small
                >
              </div>
              <StatusTag
                :label="booking.status || '已预约'"
                :tone="bookingTone(booking.status)"
                size="small"
              />
              <button
                v-if="booking.status === '已预约'"
                class="btn btn-sm"
                :disabled="actionLoading === `start-${booking.bookingId}`"
                @click="startUse(booking)"
              >
                {{ actionLoading === `start-${booking.bookingId}` ? '开始中…' : '开始使用' }}
              </button>
              <button
                v-else-if="booking.status === '使用中'"
                class="btn btn-sm"
                :disabled="actionLoading === `finish-${booking.bookingId}`"
                @click="finishUse(booking)"
              >
                {{ actionLoading === `finish-${booking.bookingId}` ? '结束中…' : '结束使用' }}
              </button>
            </article>
            <p v-if="!bookings.length">暂无预约记录。</p>
          </section></template
        >
        <template v-else
          ><template v-if="selectedResource"
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
            </button></template
          ><InlineState v-else empty empty-text="从左侧选择设施或物品" />
          <section class="loan-ledger">
            <header>
              <span>LOAN HISTORY</span><strong>我的借用 {{ loans.length }} 笔</strong>
            </header>
            <article
              v-for="loan in loans"
              :key="loan.loanId"
              :class="{ overdue: isLoanOverdue(loan) }"
            >
              <div>
                <b>物品 #{{ loan.itemId }}</b
                ><small
                  >借用单 #{{ loan.loanId }} · 借 {{ formatLoanDate(loan.borrowTime) }} · 应还
                  {{ formatLoanDate(loan.dueTime)
                  }}{{ loan.returnTime ? ` · 已还 ${formatLoanDate(loan.returnTime)}` : '' }}</small
                ><small v-if="isLoanOverdue(loan)" class="overdue-hint"
                  >已超期，归还将扣 2 信用分</small
                >
              </div>
              <StatusTag :label="loanStatus(loan)" :tone="loanTone(loan)" size="small" />
              <button
                v-if="!loan.returnTime"
                class="btn btn-sm"
                :disabled="actionLoading === `return-${loan.loanId}`"
                @click="returnLoan(loan)"
              >
                {{ actionLoading === `return-${loan.loanId}` ? '归还中…' : '确认归还' }}
              </button>
            </article>
            <p v-if="!loans.length">暂无借用记录。</p>
          </section></template
        >
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
  border-color: transparent;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-weight: 850;
}
.resource-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(300px, 0.62fr);
  gap: 16px;
}
.resource-browser,
.schedule-panel {
  background: #fff;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.resource-browser > header,
.schedule-panel > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 92px;
  padding: 30px 34px 12px;
  border-bottom: 0;
}
.resource-browser header span,
.schedule-panel header span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.resource-browser h2,
.schedule-panel h2 {
  margin: 6px 0 0;
  font-family: var(--font-display);
  font-size: 30px;
  font-weight: 950;
}
.resource-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 16px;
  padding: 0 24px 24px;
}
.resource-grid > button {
  display: grid;
  min-height: 160px;
  padding: 22px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  text-align: left;
  cursor: pointer;
}
.resource-grid > button:hover,
.resource-grid > button.active {
  background: var(--color-brand-soft);
}
.resource-grid > button.active {
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.12);
}
.resource-grid > button > span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
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
  font-size: 15px;
  line-height: 1.7;
}
.resource-grid footer {
  display: flex;
  align-items: end;
  justify-content: space-between;
  margin-top: 14px;
}
.resource-grid footer small {
  color: var(--color-text-muted);
  font-size: 14px;
}
.schedule-panel {
  background: #fff;
  color: var(--color-text);
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.schedule-panel > header {
  border-color: transparent;
}
.schedule-note {
  margin: 14px 18px 0;
  color: var(--color-text-muted);
  font-size: 15px;
  line-height: 1.7;
}
.date-line {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  padding: 18px 18px 0;
  gap: 7px;
}
.date-line button {
  padding: 12px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  color: var(--color-text-muted);
  font-size: 15px;
  font-weight: 800;
}
.date-line button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}
.time-slots {
  display: grid;
  grid-template-columns: 1fr 1fr;
  padding: 13px 18px;
  gap: 7px;
}
.time-slots button {
  display: grid;
  padding: 16px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  color: var(--color-ink);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  text-align: left;
  gap: 8px;
  cursor: pointer;
}
.time-slots button small {
  color: var(--color-text-muted);
  font: inherit;
  font-size: 14px;
}
.time-slots button:disabled {
  opacity: 0.4;
}
.time-slots button.active {
  background: var(--color-brand-soft);
  color: var(--color-brand);
}
.schedule-feedback {
  margin: 13px 18px 0;
  padding: 14px 16px;
  border-left: 0;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 800;
}
.global-feedback {
  margin: 14px 0;
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
.loan-ledger article.overdue {
  background: #fff1f0;
  border-radius: var(--radius-lg);
  padding-left: 10px;
  padding-right: 10px;
}
.loan-ledger .overdue-hint {
  color: var(--color-danger);
  font-weight: 800;
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
.date-pick {
  display: flex;
  align-items: center;
  justify-content: center;
  margin: 2px 0 12px;
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
  font-weight: 700;
  gap: 8px;
}
.date-pick input[type='date'] {
  padding: 6px 10px;
  border: 1px solid var(--color-line-strong, #d5e0ef);
  border-radius: 8px;
  background: #fff;
  color: var(--color-ink, #12233f);
  font: 600 14px inherit;
}
.time-slots button.taken {
  background: var(--color-danger-soft, #fbece9);
  color: var(--color-danger, #c0392b);
  cursor: not-allowed;
  opacity: 0.92;
}
</style>
