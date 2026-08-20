<script setup>
import { computed, onMounted, ref } from 'vue'
import { counselorApi } from '@/api/counselor'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const loading = ref(true),
  error = ref(''),
  applications = ref([]),
  stats = ref({}),
  selected = ref(null),
  rejectReason = ref(''),
  working = ref(false),
  feedback = ref('')
const statusOf = (item) => String(item.status ?? item.applyStatus ?? '待批')
const pending = computed(() =>
  applications.value.filter((item) => /待批|pending/i.test(statusOf(item)))
)
const metrics = computed(() => [
  { label: '待审批', value: pending.value.length, hint: '当前队列' },
  {
    label: '已通过',
    value:
      stats.value.approvedCount ??
      applications.value.filter((x) => /通过/.test(statusOf(x))).length,
    hint: '本期'
  },
  {
    label: '计划离校',
    value: stats.value.awayCount ?? stats.value.leaveCount ?? '—',
    hint: '统计口径'
  }
])
const load = async () => {
  loading.value = true
  error.value = ''
  const [listResult, statsResult] = await Promise.allSettled([
    counselorApi.getLeaveApplications({ page: 1, pageSize: 50 }),
    counselorApi.getLeaveStats()
  ])
  if (listResult.status === 'fulfilled') {
    applications.value = normalizeCollection(listResult.value).items
    selected.value = pending.value[0] ?? applications.value[0] ?? null
  } else error.value = toUserMessage(listResult.reason, '离校审批暂时无法同步')
  if (statsResult.status === 'fulfilled') stats.value = statsResult.value ?? {}
  loading.value = false
}
const decide = async (action) => {
  if (!selected.value) return
  working.value = true
  feedback.value = ''
  const id = selected.value.applyId ?? selected.value.applicationId
  try {
    if (action === 'approve') await counselorApi.approveLeave(id)
    else {
      if (!rejectReason.value.trim()) {
        feedback.value = '请先填写驳回原因'
        return
      }
      await counselorApi.rejectLeave(id, rejectReason.value.trim())
    }
    feedback.value = action === 'approve' ? '审批已通过' : '申请已驳回'
    rejectReason.value = ''
    await load()
  } catch (e) {
    feedback.value = toUserMessage(e, '审批操作失败')
  } finally {
    working.value = false
  }
}
onMounted(load)
</script>

<template>
  <main class="approval-page">
    <WorkspaceHeader
      eyebrow="LEAVE APPROVAL INBOX"
      title="离校审批"
      description="按返校时间和申请状态组织审批，选中一条即可查看完整行程并作出决定。"
    />
    <MetricStrip :metrics="metrics" style="--metric-count: 3" /><InlineState
      :loading="loading"
      :error="error"
    />
    <section v-if="!loading && !error" class="approval-layout">
      <aside class="inbox">
        <header>
          <span>PENDING INBOX</span><b>{{ pending.length }}</b>
        </header>
        <div class="inbox-list">
          <button
            v-for="item in applications"
            :key="item.applyId ?? item.applicationId"
            :class="{
              active:
                (selected?.applyId ?? selected?.applicationId) ===
                (item.applyId ?? item.applicationId)
            }"
            @click="selected = item"
          >
            <small>{{ statusOf(item) }} · {{ item.studentId ?? '学号待同步' }}</small
            ><strong>{{ item.studentName ?? item.destination ?? '离校申请' }}</strong>
            <p>
              {{ item.leaveDate ?? item.startDate ?? '—' }} →
              {{ item.returnDate ?? item.endDate ?? '—' }}
            </p>
          </button>
          <p v-if="!applications.length" class="empty">当前没有离校申请</p>
        </div>
      </aside>
      <article v-if="selected" class="application-sheet">
        <header>
          <div>
            <span>APPLICATION / {{ selected.applyId ?? selected.applicationId }}</span>
            <h2>{{ selected.studentName ?? selected.studentId ?? '离校申请' }}</h2>
          </div>
          <b>{{ statusOf(selected) }}</b>
        </header>
        <dl>
          <div>
            <dt>目的地</dt>
            <dd>{{ selected.destination ?? '—' }}</dd>
          </div>
          <div>
            <dt>离校日期</dt>
            <dd>{{ selected.leaveDate ?? selected.startDate ?? '—' }}</dd>
          </div>
          <div>
            <dt>计划返校</dt>
            <dd>{{ selected.returnDate ?? selected.endDate ?? '—' }}</dd>
          </div>
          <div>
            <dt>联系方式</dt>
            <dd>{{ selected.phone ?? selected.contactPhone ?? '—' }}</dd>
          </div>
        </dl>
        <section>
          <span>REASON</span>
          <p>{{ selected.reason ?? selected.leaveReason ?? '未填写补充说明' }}</p>
        </section>
        <footer>
          <button class="btn btn-primary" :disabled="working" @click="decide('approve')">
            通过申请
          </button>
          <div>
            <input v-model="rejectReason" placeholder="驳回时填写原因" /><button
              class="btn"
              :disabled="working"
              @click="decide('reject')"
            >
              驳回
            </button>
          </div>
        </footer>
        <p v-if="feedback" class="feedback">{{ feedback }}</p>
      </article>
      <div v-else class="placeholder">暂无待审申请</div>
    </section>
  </main>
</template>

<style scoped>
.approval-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.approval-layout {
  display: grid;
  grid-template-columns: minmax(280px, 320px) minmax(0, 1fr);
  height: 640px;
  margin-top: 28px;
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.inbox {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
  background: var(--color-surface-muted);
}
.inbox header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  padding: 20px 20px 14px;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}
.inbox header span,
.inbox header b {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.inbox-list {
  min-height: 0;
  flex: 1;
  overflow-y: auto;
  padding: 0 0 12px;
  scrollbar-color: var(--color-brand-border) transparent;
  scrollbar-width: thin;
}
.inbox button {
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
.inbox button.active {
  background: var(--color-brand-soft);
  color: var(--color-ink);
  box-shadow: 0 14px 28px rgba(11, 99, 199, 0.12);
}
.inbox small {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}
.inbox strong {
  color: var(--color-ink);
  font-size: 16px;
  font-weight: 800;
}
.inbox p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.7;
}
.application-sheet {
  display: flex;
  flex-direction: column;
  min-width: 0;
  min-height: 0;
  padding: 32px;
  background: #fff;
}
.application-sheet > header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 20px;
  padding-bottom: 22px;
}
.application-sheet header span,
.application-sheet section > span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  line-height: 1.4;
  letter-spacing: 0;
}
.application-sheet h2 {
  margin: 9px 0 0;
  color: var(--color-ink);
  font: 900 30px var(--font-display);
  line-height: 1.2;
}
.application-sheet header b {
  align-self: start;
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.application-sheet dl {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 12px;
  margin: 0;
}
.application-sheet dl div {
  min-width: 0;
  padding: 16px 18px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.application-sheet dt {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}
.application-sheet dd {
  margin: 7px 0 0;
  overflow-wrap: anywhere;
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 800;
  line-height: 1.5;
}
.application-sheet section {
  padding: 24px 0 18px;
}
.application-sheet section p {
  margin: 8px 0 0;
  color: var(--color-text-muted);
  font-size: 14px;
  line-height: 1.8;
}
.application-sheet footer {
  display: grid;
  grid-template-columns: minmax(170px, 220px) minmax(0, 1fr);
  gap: 16px;
  align-items: start;
  margin-top: auto;
  padding-top: 18px;
}
.application-sheet footer div {
  display: grid;
  grid-template-columns: minmax(0, 1fr) auto;
  gap: 12px;
  min-width: 0;
}
.application-sheet input {
  width: 100%;
  min-width: 0;
  min-height: 48px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 14px;
}
.application-sheet footer > .btn,
.application-sheet footer div .btn {
  min-height: 48px;
}
.application-sheet input:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}
.feedback {
  margin: 16px 0 0;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 13px;
}
.placeholder,
.empty {
  display: grid;
  place-items: center;
  color: var(--color-text-muted);
  font-size: 13px;
}
@media (max-width: 800px) {
  .approval-layout {
    grid-template-columns: 1fr;
    height: auto;
  }
  .inbox {
    border-right: 0;
    padding-bottom: 8px;
  }
  .inbox-list {
    max-height: 420px;
  }
  .application-sheet dl {
    grid-template-columns: 1fr 1fr;
  }
  .application-sheet footer {
    grid-template-columns: 1fr;
  }
  .application-sheet footer div {
    grid-template-columns: 1fr;
  }
  .application-sheet input {
    min-width: 0;
  }
}
@media (max-width: 520px) {
  .approval-page {
    width: min(100% - 32px, var(--content-max));
  }
  .application-sheet {
    padding: 24px 18px;
  }
  .application-sheet dl {
    grid-template-columns: 1fr;
  }
}
</style>
