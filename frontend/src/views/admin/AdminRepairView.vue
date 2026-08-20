<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const store = useUserStore(),
  loading = ref(true),
  error = ref(''),
  tickets = ref([]),
  selected = ref(null),
  working = ref(false),
  feedback = ref('')
const completion = ref({ content: '', repairResult: '已修复' })
const state = (item) => String(item.status ?? item.ticketStatus ?? '待处理')
const selectTicket = (item) => {
  selected.value = item
  feedback.value = ''
}
const metrics = computed(() => [
  { label: '工单总量', value: tickets.value.length, hint: '当前队列' },
  {
    label: '待响应',
    value: tickets.value.filter((x) => /待|派单|pending/i.test(state(x))).length,
    hint: '需优先'
  },
  {
    label: '处理中',
    value: tickets.value.filter((x) => /处理中|维修|claimed/i.test(state(x))).length,
    hint: '执行中'
  }
])
const load = async () => {
  loading.value = true
  error.value = ''
  try {
    const data = await adminApi.getRepairTickets(store.userInfo?.id, { page: 1, pageSize: 40 })
    tickets.value = normalizeCollection(data).items
    selected.value = tickets.value[0] ?? null
  } catch (e) {
    error.value = toUserMessage(e, '维修队列暂时无法同步')
  } finally {
    loading.value = false
  }
}
const claim = async () => {
  if (!selected.value) return
  working.value = true
  feedback.value = ''
  try {
    await adminApi.claimRepairTicket(selected.value.ticketId)
    feedback.value = '已接单'
    await load()
  } catch (e) {
    feedback.value = toUserMessage(e, '接单失败')
  } finally {
    working.value = false
  }
}
const complete = async () => {
  if (!selected.value || !completion.value.content.trim()) return
  working.value = true
  feedback.value = ''
  try {
    await adminApi.completeRepairTicket(selected.value.ticketId, completion.value)
    feedback.value = '维修结果已登记'
    completion.value.content = ''
    await load()
  } catch (e) {
    feedback.value = toUserMessage(e, '完工登记失败')
  } finally {
    working.value = false
  }
}
onMounted(load)
</script>

<template>
  <main class="repair-page">
    <WorkspaceHeader
      eyebrow="REPAIR DISPATCH"
      title="维修调度"
      description="左侧按 SLA 浏览工单，右侧聚焦一张工单完成接单与维修闭环。"
      ><button class="btn btn-sm" @click="load">刷新队列</button></WorkspaceHeader
    >
    <MetricStrip :metrics="metrics" style="--metric-count: 3" /><InlineState
      :loading="loading"
      :error="error"
    />
    <section v-if="!loading && !error" class="repair-board">
      <aside class="ticket-queue">
        <header>
          <span>QUEUE</span><b>{{ tickets.length }}</b>
        </header>
        <button
          v-for="item in tickets"
          :key="item.ticketId"
          :class="{ active: selected?.ticketId === item.ticketId }"
          @click="selectTicket(item)"
        >
          <small>#{{ item.ticketId }} · {{ state(item) }}</small
          ><strong>{{ item.title ?? item.category ?? item.repairType ?? '宿舍报修' }}</strong>
          <p>{{ item.roomName ?? item.location ?? '位置待确认' }}</p>
        </button>
        <p v-if="!tickets.length" class="empty">暂无待处理工单</p>
      </aside>
      <article v-if="selected" class="ticket-sheet">
        <header>
          <div>
            <span>WORK ORDER #{{ selected.ticketId }}</span>
            <h2>{{ selected.title ?? selected.category ?? selected.repairType ?? '宿舍报修' }}</h2>
          </div>
          <b>{{ state(selected) }}</b>
        </header>
        <dl>
          <div>
            <dt>报修位置</dt>
            <dd>{{ selected.roomName ?? selected.location ?? '—' }}</dd>
          </div>
          <div>
            <dt>提交时间</dt>
            <dd>{{ selected.createdAt ?? selected.submitTime ?? '—' }}</dd>
          </div>
          <div>
            <dt>报修人</dt>
            <dd>{{ selected.studentName ?? selected.studentId ?? '—' }}</dd>
          </div>
        </dl>
        <section>
          <span>ISSUE DESCRIPTION</span>
          <p>{{ selected.description ?? selected.content ?? '暂无补充描述' }}</p>
        </section>
        <div class="actions">
          <button class="btn" :disabled="working" @click="claim">接收工单</button>
          <form @submit.prevent="complete">
            <select v-model="completion.repairResult">
              <option>已修复</option>
              <option>需更换配件</option>
              <option>无法修复</option></select
            ><button class="btn btn-primary" :disabled="working">登记完工</button
            ><textarea
              v-model="completion.content"
              required
              rows="3"
              placeholder="填写维修过程与结果"
            ></textarea>
          </form>
        </div>
        <p v-if="feedback" class="feedback">{{ feedback }}</p>
      </article>
      <div v-else class="ticket-placeholder">从左侧选择一张工单查看详情</div>
    </section>
  </main>
</template>

<style scoped>
.repair-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.repair-board {
  display: grid;
  grid-template-columns: 300px 1fr;
  min-height: 520px;
  margin-top: 28px;
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.ticket-queue {
  background: var(--color-surface-muted);
}
.ticket-queue header {
  display: flex;
  justify-content: space-between;
  padding: 18px 20px 12px;
  color: var(--color-brand-strong);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.ticket-queue header span,
.ticket-queue header b {
  color: var(--color-brand) !important;
  font-family: var(--font-body) !important;
  font-size: 15px !important;
  font-weight: 850 !important;
  letter-spacing: 0 !important;
}
.ticket-queue button {
  display: flex;
  width: calc(100% - 24px);
  flex-direction: column;
  align-items: start;
  gap: 7px;
  margin: 0 12px 12px;
  padding: 16px 16px 18px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  text-align: left;
  cursor: pointer;
  box-shadow: 0 10px 22px rgba(23, 65, 120, 0.06);
}
.ticket-queue button.active {
  background: var(--color-brand-soft);
  color: var(--color-ink);
  box-shadow: 0 14px 28px rgba(11, 99, 199, 0.12);
}
.ticket-queue small {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.ticket-queue strong {
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 800;
}
.ticket-queue p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.7;
}
.ticket-sheet {
  padding: 32px;
  background: #fff;
}
.ticket-sheet > header {
  display: flex;
  justify-content: space-between;
  gap: 20px;
  padding-bottom: 18px;
}
.ticket-sheet header span,
.ticket-sheet section > span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.ticket-sheet h2 {
  margin: 9px 0 0;
  color: var(--color-ink);
  font: 900 30px var(--font-display);
}
.ticket-sheet header > b {
  align-self: start;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.ticket-sheet dl {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 12px;
  margin: 0;
}
.ticket-sheet dl div {
  padding: 16px 18px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.ticket-sheet dt {
  color: var(--color-text-soft);
  font-size: 12px;
  font-weight: 700;
}
.ticket-sheet dd {
  margin: 7px 0 0;
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 800;
}
.ticket-sheet section {
  padding: 24px 0 18px;
}
.ticket-sheet section p {
  margin: 8px 0 0;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.8;
}
.actions {
  display: grid;
  grid-template-columns: 1fr;
  gap: 16px;
  align-items: start;
}
.actions > .btn {
  width: 100%;
  min-height: 48px;
}
.actions form {
  display: grid;
  grid-template-columns: minmax(160px, 220px) minmax(0, 1fr);
  gap: 12px;
  min-width: 0;
  width: 100%;
}
.actions select,
.actions form button,
.actions textarea {
  min-width: 0;
  width: 100%;
}
.actions form button {
  grid-column: 2;
}
.actions select,
.actions textarea {
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font: 14px var(--font-body);
}
.actions textarea {
  grid-column: 1/-1;
  min-height: 120px;
  resize: vertical;
}
.actions select:focus-visible,
.actions textarea:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}
.feedback {
  margin-top: 16px;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 13px;
}
.ticket-placeholder,
.empty {
  display: grid;
  place-items: center;
  color: var(--color-text-muted);
  font-size: 13px;
}
@media (max-width: 800px) {
  .repair-board {
    grid-template-columns: 1fr;
  }
  .ticket-queue {
    border-right: 0;
  }
  .ticket-sheet dl,
  .actions,
  .actions form {
    grid-template-columns: 1fr;
  }
  .actions form button,
  .actions textarea {
    grid-column: auto;
  }
}
</style>
