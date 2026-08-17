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
          @click="selected = item, feedback = ''"
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
            ><textarea
              v-model="completion.content"
              required
              rows="3"
              placeholder="填写维修过程与结果"
            ></textarea
            ><button class="btn btn-primary" :disabled="working">登记完工</button>
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
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.repair-board {
  display: grid;
  grid-template-columns: 300px 1fr;
  min-height: 520px;
  margin-top: 28px;
  border: 1px solid var(--color-line-strong);
}
.ticket-queue {
  border-right: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.25);
}
.ticket-queue header {
  display: flex;
  justify-content: space-between;
  padding: 17px 20px;
  border-bottom: 1px solid var(--color-line);
  font: 9px var(--font-mono);
}
.ticket-queue button {
  display: flex;
  width: 100%;
  flex-direction: column;
  align-items: start;
  gap: 7px;
  padding: 18px 20px;
  border: 0;
  border-bottom: 1px solid var(--color-line);
  background: transparent;
  text-align: left;
  cursor: pointer;
}
.ticket-queue button.active {
  background: var(--color-ink);
  color: #fff;
}
.ticket-queue small {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.ticket-queue strong {
  font-size: 12px;
}
.ticket-queue p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 10px;
}
.ticket-sheet {
  padding: 34px;
}
.ticket-sheet > header {
  display: flex;
  justify-content: space-between;
  border-bottom: 1px solid var(--color-line-strong);
  padding-bottom: 24px;
}
.ticket-sheet header span,
.ticket-sheet section > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.ticket-sheet h2 {
  margin: 9px 0 0;
  font: 500 30px var(--font-display);
}
.ticket-sheet header > b {
  font-size: 10px;
  color: var(--color-accent-strong);
}
.ticket-sheet dl {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  margin: 0;
  border-bottom: 1px solid var(--color-line);
}
.ticket-sheet dl div {
  padding: 18px;
  border-right: 1px solid var(--color-line);
}
.ticket-sheet dt {
  color: var(--color-text-soft);
  font-size: 9px;
}
.ticket-sheet dd {
  margin: 7px 0 0;
  font-size: 12px;
}
.ticket-sheet section {
  padding: 24px 0;
}
.ticket-sheet section p {
  font-size: 12px;
  line-height: 1.8;
}
.actions {
  display: grid;
  grid-template-columns: 140px 1fr;
  gap: 20px;
  align-items: start;
}
.actions form {
  display: grid;
  grid-template-columns: 160px 1fr;
  gap: 10px;
}
.actions textarea {
  grid-column: 1/-1;
}
.actions select,
.actions textarea {
  padding: 11px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  font: 11px var(--font-body);
}
.feedback {
  color: var(--color-accent-strong);
  font-size: 11px;
}
.ticket-placeholder,
.empty {
  display: grid;
  place-items: center;
  color: var(--color-text-muted);
  font-size: 11px;
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
}
</style>
