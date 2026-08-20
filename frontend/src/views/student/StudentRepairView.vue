<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const loading = ref(true)
const submitting = ref(false)
const error = ref('')
const feedback = ref('')
const tickets = ref([])
const description = ref('')
const urgency = ref('普通')
const activeTicket = ref(null)
const studentId = computed(() => userStore.userInfo?.id || '')
const openTickets = computed(() =>
  tickets.value.filter(
    (item) => !['已完成', '已撤销', 'completed', 'cancelled'].includes(item.status)
  )
)

const loadTickets = async () => {
  loading.value = true
  error.value = ''
  try {
    tickets.value = normalizeCollection(await studentApi.getRepairTickets(studentId.value)).items
    activeTicket.value = activeTicket.value || tickets.value[0] || null
  } catch (requestError) {
    error.value = toUserMessage(requestError, '报修记录暂时无法同步')
  } finally {
    loading.value = false
  }
}
const submitRepair = async () => {
  if (!description.value.trim()) return
  submitting.value = true
  feedback.value = ''
  try {
    await studentApi.createRepairTicket({
      description: description.value.trim(),
      urgency: urgency.value
    })
    description.value = ''
    feedback.value = '报修已提交，系统将自动进入派单队列'
    await loadTickets()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '提交失败，请稍后重试')
  } finally {
    submitting.value = false
  }
}
const tone = (status) =>
  ['已完成', 'completed'].includes(status)
    ? 'success'
    : ['已撤销', 'cancelled'].includes(status)
      ? 'neutral'
      : 'warning'
onMounted(loadTickets)
</script>

<template>
  <div class="repair-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / REPAIR"
      title="报修与处理进度"
      description="从问题描述到维修结果，所有状态变化集中在一条时间线上。"
    >
      <button
        class="btn btn-primary"
        type="button"
        @click="description ||= '请描述具体位置和故障现象'"
      >
        发起报修
      </button>
    </WorkspaceHeader>
    <section class="repair-summary">
      <article>
        <span>进行中</span><strong>{{ openTickets.length }}</strong
        ><small>等待处理或维修中</small>
      </article>
      <article>
        <span>全部记录</span><strong>{{ tickets.length }}</strong
        ><small>含完成与撤销</small>
      </article>
      <article><span>处理承诺</span><strong>4h</strong><small>普通工单目标响应</small></article>
    </section>
    <div class="repair-layout">
      <section class="repair-compose">
        <header>
          <span>NEW REQUEST</span>
          <h2>描述宿舍问题</h2>
          <p>尽量写清房间位置、故障现象和影响范围。</p>
        </header>
        <form @submit.prevent="submitRepair">
          <label
            >紧急程度<select v-model="urgency" class="form-select">
              <option>普通</option>
              <option>紧急</option>
            </select></label
          ><label
            >问题描述<textarea
              v-model="description"
              class="form-input"
              rows="6"
              placeholder="例如：书桌右侧插座松动，使用时有火花…"
            ></textarea>
          </label>
          <p v-if="feedback" role="status">{{ feedback }}</p>
          <button class="btn btn-primary" :disabled="submitting || !description.trim()">
            {{ submitting ? '提交中…' : '提交报修' }}
          </button>
        </form>
      </section>
      <section class="ticket-queue">
        <header>
          <div>
            <span>MY TICKETS / {{ tickets.length }}</span>
            <h2>我的报修单</h2>
          </div>
          <button class="btn btn-sm" @click="loadTickets">刷新</button>
        </header>
        <InlineState
          :loading="loading"
          :error="error"
          :empty="!loading && !tickets.length"
          empty-text="暂无报修记录"
        /><button
          v-for="ticket in tickets"
          :key="ticket.ticketId"
          class="ticket-row"
          :class="{ active: activeTicket?.ticketId === ticket.ticketId }"
          @click="activeTicket = ticket"
        >
          <time>#{{ ticket.ticketId }}</time>
          <div>
            <b>{{ ticket.issueDesc || ticket.description || '报修事项' }}</b
            ><small>{{ ticket.createTime || ticket.createdAt || '时间待同步' }}</small>
          </div>
          <StatusTag :label="ticket.status || '待处理'" :tone="tone(ticket.status)" size="small" />
        </button>
      </section>
      <aside class="repair-timeline">
        <header>
          <span>PROCESS / LIVE</span>
          <h2>处理时间线</h2>
        </header>
        <template v-if="activeTicket"
          ><div class="timeline-step done">
            <i></i><b>报修已提交</b><span>系统记录问题并生成工单</span>
          </div>
          <div class="timeline-step" :class="{ done: activeTicket.status !== '待处理' }">
            <i></i><b>等待派单</b><span>根据区域与负载匹配维修员</span>
          </div>
          <div
            class="timeline-step"
            :class="{ done: ['已完成', 'completed'].includes(activeTicket.status) }"
          >
            <i></i><b>维修处理</b><span>维修员更新过程与材料记录</span>
          </div>
          <div class="timeline-step">
            <i></i><b>结果归档</b><span>完成后可回看维修结果</span>
          </div></template
        ><InlineState v-else empty empty-text="选择一张工单查看处理过程" />
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
.repair-summary {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  margin: 27px 0;
  border-block: 1px solid var(--color-line-strong);
}
.repair-summary article {
  display: grid;
  grid-template-columns: 1fr auto;
  padding: 17px 20px;
  border-right: 1px solid var(--color-line);
  gap: 5px;
}
.repair-summary span {
  grid-column: 1/-1;
  color: var(--color-text-muted);
  font-size: 9px;
}
.repair-summary strong {
  font-family: var(--font-display);
  font-size: 27px;
  font-weight: 500;
}
.repair-summary small {
  align-self: end;
  color: var(--color-text-soft);
  font-size: 8px;
}
.repair-layout {
  display: grid;
  grid-template-columns: minmax(260px, 0.58fr) minmax(0, 1fr) minmax(240px, 0.55fr);
  gap: 14px;
}
.repair-compose,
.ticket-queue,
.repair-timeline {
  background: #fff;
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.repair-compose {
  padding: 30px;
}
.repair-compose header > span,
.ticket-queue header span,
.repair-timeline header span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.repair-compose h2,
.ticket-queue h2,
.repair-timeline h2 {
  margin: 6px 0;
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 950;
}
.repair-compose header p {
  color: var(--color-text-muted);
  font-size: 16px;
  line-height: 1.8;
}
.repair-compose form {
  display: grid;
  margin-top: 21px;
  gap: 14px;
}
.repair-compose label {
  display: grid;
  color: var(--color-text-muted);
  font-size: 16px;
  gap: 10px;
}
.repair-compose textarea {
  resize: vertical;
}
.repair-compose form p {
  margin: 0;
  color: var(--color-brand);
  font-size: 15px;
}
.ticket-queue > header,
.repair-timeline > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 92px;
  padding: 28px 30px 12px;
  border-bottom: 0;
}
.ticket-row {
  display: grid;
  width: calc(100% - 48px);
  grid-template-columns: 58px 1fr auto;
  align-items: center;
  min-height: 92px;
  margin: 14px 24px;
  padding: 18px 20px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  text-align: left;
  gap: 16px;
  cursor: pointer;
}
.ticket-row:hover,
.ticket-row.active {
  background: var(--color-brand-soft);
}
.ticket-row.active {
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.12);
}
.ticket-row time {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
}
.ticket-row div {
  display: grid;
  gap: 5px;
}
.ticket-row b {
  font-family: var(--font-display);
  font-size: 18px;
  font-weight: 900;
}
.ticket-row small {
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.6;
}
.repair-timeline {
  padding-bottom: 18px;
  background: #fff;
  color: var(--color-text);
}
.repair-timeline > header {
  border-color: transparent;
}
.timeline-step {
  position: relative;
  display: grid;
  margin-left: 28px;
  padding: 18px 18px 5px 28px;
  color: var(--color-text-muted);
}
.timeline-step::before {
  position: absolute;
  top: 29px;
  bottom: -23px;
  left: 3px;
  width: 1px;
  background: #41554a;
  content: '';
}
.timeline-step:last-of-type::before {
  display: none;
}
.timeline-step i {
  position: absolute;
  top: 23px;
  left: -1px;
  width: 9px;
  height: 9px;
  border: 1px solid var(--color-brand-border);
  border-radius: 50%;
  background: #fff;
}
.timeline-step.done {
  color: var(--color-ink);
}
.timeline-step.done i {
  border-color: var(--color-brand);
  background: var(--color-brand);
}
.timeline-step b {
  font-family: var(--font-display);
  font-size: 12px;
}
.timeline-step span {
  margin-top: 5px;
  color: #819388;
  font-size: 8px;
  line-height: 1.5;
}
@media (max-width: 1000px) {
  .repair-layout {
    grid-template-columns: 1fr 1fr;
  }
  .repair-timeline {
    grid-column: 1/-1;
  }
}
@media (max-width: 720px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .repair-summary,
  .repair-layout {
    grid-template-columns: 1fr;
  }
  .repair-summary article {
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
  .repair-timeline {
    grid-column: auto;
  }
}
</style>
