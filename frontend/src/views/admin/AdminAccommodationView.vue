<script setup>
import { computed, ref } from 'vue'
import { accommodationApi } from '@/api/accommodation'
import { MetricStrip, WorkspaceHeader } from '@/components'
import { toUserMessage } from '@/utils/errorMessage'

const today = new Date().toISOString().slice(0, 10)
const allocation = ref({ studentId: '', roomId: '', bedNo: '', checkInDate: today })
const transfer = ref({ allocationId: '', targetRoomId: '', targetBedNo: '' })
const roomLookup = ref('')
const occupants = ref([])
const roomSummary = ref(null)
const checkout = ref({ allocationId: '', checkoutId: '', reason: '', checkoutDate: today })
const checkoutSummary = ref(null)
const working = ref('')
const feedback = ref({ type: '', text: '' })

const metrics = computed(() => [
  {
    label: '当前住户',
    value: occupants.value.length || '—',
    hint: roomSummary.value ? `房间 ${roomSummary.value.roomId}` : '先查房间'
  },
  { label: '分配编号', value: transfer.value.allocationId || '—', hint: '入住 / 调寝' },
  {
    label: '清算编号',
    value: checkout.value.checkoutId || '—',
    hint: checkoutSummary.value?.status || '退宿状态机'
  }
])

const run = async (key, action, success) => {
  working.value = key
  feedback.value = { type: '', text: '' }
  try {
    const data = await action()
    feedback.value = { type: 'success', text: success }
    return data
  } catch (error) {
    feedback.value = {
      type: 'error',
      text: toUserMessage(error, '操作失败，请核对编号与状态后重试')
    }
    return null
  } finally {
    working.value = ''
  }
}

const createAllocation = async () => {
  const data = await run(
    'allocation',
    () =>
      accommodationApi.createAllocation({
        studentId: allocation.value.studentId,
        roomId: Number(allocation.value.roomId),
        bedNo: Number(allocation.value.bedNo),
        checkInDate: allocation.value.checkInDate
      }),
    '入住分配已完成'
  )
  const allocationId = data?.allocationId ?? data?.id
  if (allocationId) {
    transfer.value.allocationId = String(allocationId)
    checkout.value.allocationId = String(allocationId)
  }
}

const transferAllocation = () =>
  run(
    'transfer',
    () =>
      accommodationApi.transferAllocation(transfer.value.allocationId, {
        targetRoomId: Number(transfer.value.targetRoomId),
        targetBedNo: Number(transfer.value.targetBedNo)
      }),
    '调寝已完成，新旧床位已同步更新'
  )

const loadOccupants = async () => {
  const data = await run(
    'occupants',
    () => accommodationApi.getRoomOccupants(roomLookup.value),
    '房间住户已刷新'
  )
  if (data) {
    occupants.value = Array.isArray(data.items) ? data.items : []
    roomSummary.value = data
  }
}

const registerCheckout = async () => {
  const data = await run(
    'register',
    () =>
      accommodationApi.registerCheckout(checkout.value.allocationId, {
        reason: checkout.value.reason || null,
        checkoutDate: checkout.value.checkoutDate || null
      }),
    '退宿已登记，下一步可执行清算校验'
  )
  if (data?.checkoutId) {
    checkout.value.checkoutId = String(data.checkoutId)
    checkoutSummary.value = data
  }
}

const loadCheckout = async (success = '清算单状态已刷新') => {
  const data = await run(
    'checkout',
    () => accommodationApi.getCheckout(checkout.value.checkoutId),
    success
  )
  if (data) checkoutSummary.value = data
}

const transitionCheckout = async (action) => {
  const handlers = {
    settle: () => accommodationApi.settleCheckout(checkout.value.checkoutId),
    confirm: () =>
      accommodationApi.confirmCheckout(checkout.value.checkoutId, {
        checkoutDate: checkout.value.checkoutDate || null
      }),
    cancel: () => accommodationApi.cancelCheckout(checkout.value.checkoutId)
  }
  const labels = {
    settle: '三步清算校验已执行',
    confirm: '退宿已确认，床位已释放',
    cancel: '退宿办理已取消'
  }
  const data = await run(action, handlers[action], labels[action])
  if (data) checkoutSummary.value = data
}
</script>

<template>
  <main class="accommodation-page">
    <WorkspaceHeader
      eyebrow="ACCOMMODATION CONTROL"
      title="住宿管理"
      description="把入住、调寝、住户核对和退宿清算放进一条可追踪的办理链路。"
    >
      <span class="workflow-note">DORM-08—11 / 35—38</span>
    </WorkspaceHeader>

    <MetricStrip :metrics="metrics" style="--metric-count: 3" />
    <p v-if="feedback.text" class="feedback" :class="`feedback--${feedback.type}`">
      {{ feedback.text }}
    </p>

    <section class="operation-grid">
      <article class="operation-card operation-card--dark">
        <header>
          <span>01 / CHECK IN</span>
          <h2>入住分配</h2>
          <b>建立在住关系</b>
        </header>
        <form @submit.prevent="createAllocation">
          <label
            >学生学号<input
              v-model.trim="allocation.studentId"
              required
              placeholder="如 TST_STU_81501"
          /></label>
          <label>房间 ID<input v-model="allocation.roomId" required min="1" type="number" /></label>
          <label
            >床位号<input v-model="allocation.bedNo" required min="1" max="99" type="number"
          /></label>
          <label>入住日期<input v-model="allocation.checkInDate" required type="date" /></label>
          <button class="btn btn-primary" :disabled="working === 'allocation'">
            {{ working === 'allocation' ? '分配中…' : '确认入住分配' }}
          </button>
        </form>
      </article>

      <article class="operation-card">
        <header>
          <span>02 / TRANSFER</span>
          <h2>调寝办理</h2>
          <b>原子切换床位</b>
        </header>
        <form @submit.prevent="transferAllocation">
          <label
            >住宿分配 ID<input v-model="transfer.allocationId" required min="1" type="number"
          /></label>
          <label
            >目标房间 ID<input v-model="transfer.targetRoomId" required min="1" type="number"
          /></label>
          <label
            >目标床位号<input
              v-model="transfer.targetBedNo"
              required
              min="1"
              max="99"
              type="number"
          /></label>
          <button class="btn" :disabled="working === 'transfer'">
            {{ working === 'transfer' ? '办理中…' : '执行调寝' }}
          </button>
        </form>
      </article>

      <article class="operation-card occupant-card">
        <header>
          <span>03 / OCCUPANTS</span>
          <h2>房间住户</h2>
          <b>现场核对</b>
        </header>
        <form class="inline-form" @submit.prevent="loadOccupants">
          <input v-model="roomLookup" required min="1" type="number" placeholder="输入房间 ID" />
          <button class="btn" :disabled="working === 'occupants'">查询</button>
        </form>
        <div class="occupant-list">
          <article v-for="item in occupants" :key="`${item.studentId}-${item.bedNo}`">
            <b>{{ item.bedNo }}床</b>
            <div>
              <strong>{{ item.studentName || item.studentId }}</strong
              ><small
                >{{ item.studentId }} ·
                {{ item.checkInDate?.slice?.(0, 10) || '入住日期未知' }}</small
              >
            </div>
          </article>
          <p v-if="roomSummary && !occupants.length">该房间当前没有在住学生。</p>
          <p v-if="!roomSummary">输入房间 ID 后核对床位与住户。</p>
        </div>
      </article>
    </section>

    <section class="checkout-board">
      <header>
        <div>
          <span>04 / CHECKOUT STATE MACHINE</span>
          <h2>退宿清算</h2>
        </div>
        <p>登记 → 三步校验 → 确认退宿；未确认前可取消。</p>
      </header>
      <div class="checkout-body">
        <form class="checkout-register" @submit.prevent="registerCheckout">
          <label
            >住宿分配 ID<input v-model="checkout.allocationId" required min="1" type="number"
          /></label>
          <label>计划退宿日<input v-model="checkout.checkoutDate" type="date" /></label>
          <label class="wide"
            >备注<textarea
              v-model.trim="checkout.reason"
              rows="3"
              placeholder="可选：记录办理背景"
            ></textarea>
          </label>
          <button class="btn" :disabled="working === 'register'">登记退宿</button>
        </form>
        <div class="checkout-sheet">
          <form class="checkout-search" @submit.prevent="loadCheckout()">
            <input
              v-model="checkout.checkoutId"
              required
              min="1"
              type="number"
              placeholder="清算单 ID"
            />
            <button class="btn btn-sm">刷新状态</button>
          </form>
          <dl v-if="checkoutSummary">
            <div>
              <dt>清算编号</dt>
              <dd>#{{ checkoutSummary.checkoutId ?? checkout.checkoutId }}</dd>
            </div>
            <div>
              <dt>当前状态</dt>
              <dd>{{ checkoutSummary.status ?? '待清算' }}</dd>
            </div>
            <div>
              <dt>费用校验</dt>
              <dd>{{ checkoutSummary.feeCheck ?? '未执行' }}</dd>
            </div>
            <div>
              <dt>物品校验</dt>
              <dd>{{ checkoutSummary.itemCheck ?? '未执行' }}</dd>
            </div>
          </dl>
          <p v-else>登记后会自动带入清算单编号，也可手动输入已有编号继续办理。</p>
          <div class="checkout-actions">
            <button
              class="btn"
              :disabled="!checkout.checkoutId || working"
              @click="transitionCheckout('settle')"
            >
              执行清算
            </button>
            <button
              class="btn btn-primary"
              :disabled="!checkout.checkoutId || working"
              @click="transitionCheckout('confirm')"
            >
              确认退宿
            </button>
            <button
              class="btn btn-danger"
              :disabled="!checkout.checkoutId || working"
              @click="transitionCheckout('cancel')"
            >
              取消办理
            </button>
          </div>
        </div>
      </div>
    </section>
  </main>
</template>

<style scoped>
.accommodation-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.workflow-note {
  color: var(--color-accent-strong);
  font: 9px var(--font-mono);
  letter-spacing: 0.12em;
}
.feedback {
  margin: 18px 0 0;
  padding: 12px 16px;
  border: 1px solid var(--color-brand-border);
  font-size: 11px;
}
.feedback--success {
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
}
.feedback--error {
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.operation-grid {
  display: grid;
  grid-template-columns: 1.05fr 0.95fr 0.9fr;
  gap: 18px;
  margin-top: 28px;
}
.operation-card {
  min-height: 390px;
  padding: 26px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.25);
}
.operation-card--dark {
  background: var(--color-ink);
  color: #fff;
}
.operation-card header {
  min-height: 86px;
  border-bottom: 1px solid var(--color-line);
  position: relative;
}
.operation-card--dark header {
  border-color: rgba(255, 255, 255, 0.18);
}
.operation-card header span,
.checkout-board header span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.operation-card header h2,
.checkout-board h2 {
  margin: 8px 0 0;
  font: 500 26px var(--font-display);
}
.operation-card header b {
  position: absolute;
  right: 0;
  top: 0;
  color: var(--color-text-soft);
  font-size: 9px;
}
.operation-card form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  margin-top: 22px;
}
.operation-card label,
.checkout-register label {
  display: flex;
  flex-direction: column;
  gap: 7px;
  font-size: 9px;
  color: var(--color-text-muted);
}
.operation-card--dark label {
  color: #bcb7af;
}
.operation-card input,
.checkout-board input,
.checkout-board textarea {
  width: 100%;
  padding: 10px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 11px;
}
.operation-card--dark input {
  border-color: #4c4944;
  background: #252422;
  color: #fff;
}
.operation-card form button {
  grid-column: 1/-1;
  margin-top: 6px;
}
.inline-form {
  grid-template-columns: 1fr auto !important;
}
.inline-form button {
  grid-column: auto !important;
  margin-top: 0 !important;
}
.occupant-list {
  margin-top: 18px;
  border-top: 1px solid var(--color-line);
}
.occupant-list article {
  display: grid;
  grid-template-columns: 48px 1fr;
  gap: 12px;
  padding: 13px 0;
  border-bottom: 1px solid var(--color-line);
}
.occupant-list article > b {
  color: var(--color-accent-strong);
  font: 10px var(--font-mono);
}
.occupant-list div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.occupant-list strong {
  font-size: 11px;
}
.occupant-list small,
.occupant-list p {
  color: var(--color-text-muted);
  font-size: 9px;
}
.checkout-board {
  margin-top: 28px;
  border: 1px solid var(--color-line-strong);
}
.checkout-board > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 25px 28px;
  border-bottom: 1px solid var(--color-line-strong);
}
.checkout-board > header p {
  max-width: 420px;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 10px;
}
.checkout-body {
  display: grid;
  grid-template-columns: 0.8fr 1.2fr;
}
.checkout-register {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  padding: 26px;
  border-right: 1px solid var(--color-line);
}
.checkout-register .wide,
.checkout-register button {
  grid-column: 1/-1;
}
.checkout-sheet {
  padding: 26px;
  background: rgba(255, 255, 255, 0.24);
}
.checkout-search {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 10px;
}
.checkout-sheet dl {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  margin: 22px 0;
  border-block: 1px solid var(--color-line);
}
.checkout-sheet dl div {
  padding: 15px 10px;
  border-right: 1px solid var(--color-line);
}
.checkout-sheet dt {
  color: var(--color-text-soft);
  font-size: 8px;
}
.checkout-sheet dd {
  margin: 7px 0 0;
  font-size: 12px;
}
.checkout-sheet > p {
  margin: 22px 0;
  color: var(--color-text-muted);
  font-size: 10px;
}
.checkout-actions {
  display: flex;
  gap: 9px;
  flex-wrap: wrap;
}
@media (max-width: 980px) {
  .operation-grid {
    grid-template-columns: 1fr 1fr;
  }
  .occupant-card {
    grid-column: 1/-1;
  }
  .checkout-body {
    grid-template-columns: 1fr;
  }
  .checkout-register {
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
}
@media (max-width: 680px) {
  .operation-grid {
    grid-template-columns: 1fr;
  }
  .occupant-card {
    grid-column: auto;
  }
  .checkout-board > header {
    align-items: start;
    flex-direction: column;
    gap: 12px;
  }
  .checkout-register,
  .operation-card form {
    grid-template-columns: 1fr;
  }
  .checkout-register .wide,
  .checkout-register button,
  .operation-card form button {
    grid-column: auto;
  }
  .checkout-sheet dl {
    grid-template-columns: 1fr 1fr;
  }
}
</style>
