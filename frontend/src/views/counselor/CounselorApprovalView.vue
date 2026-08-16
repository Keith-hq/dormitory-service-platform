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
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.approval-layout {
  display: grid;
  grid-template-columns: 320px 1fr;
  min-height: 520px;
  margin-top: 30px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.25);
}
.inbox {
  border-right: 1px solid var(--color-line-strong);
}
.inbox header {
  display: flex;
  justify-content: space-between;
  padding: 18px 20px;
  font: 9px var(--font-mono);
  border-bottom: 1px solid var(--color-line);
}
.inbox button {
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
.inbox button.active {
  background: var(--color-brand-soft);
}
.inbox small {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.inbox strong {
  font-size: 12px;
}
.inbox p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 10px;
}
.application-sheet {
  padding: 36px;
}
.application-sheet > header {
  display: flex;
  justify-content: space-between;
  padding-bottom: 25px;
  border-bottom: 1px solid var(--color-line-strong);
}
.application-sheet span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.application-sheet h2 {
  margin: 10px 0 0;
  font: 500 32px var(--font-display);
}
.application-sheet header b {
  font-size: 10px;
  color: var(--color-accent-strong);
}
.application-sheet dl {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  margin: 0;
  border-bottom: 1px solid var(--color-line);
}
.application-sheet dl div {
  padding: 18px 14px;
  border-right: 1px solid var(--color-line);
}
dt {
  color: var(--color-text-soft);
  font-size: 9px;
}
dd {
  margin: 7px 0 0;
  font-size: 11px;
}
.application-sheet section {
  padding: 28px 0;
}
.application-sheet section p {
  font-size: 12px;
  line-height: 1.8;
}
.application-sheet footer {
  display: flex;
  justify-content: space-between;
  gap: 16px;
  padding-top: 20px;
  border-top: 1px solid var(--color-line-strong);
}
.application-sheet footer div {
  display: flex;
  gap: 8px;
}
.application-sheet input {
  min-width: 220px;
  padding: 10px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
}
.feedback {
  color: var(--color-accent-strong);
  font-size: 11px;
}
.placeholder,
.empty {
  display: grid;
  place-items: center;
  color: var(--color-text-muted);
  font-size: 11px;
}
@media (max-width: 800px) {
  .approval-layout {
    grid-template-columns: 1fr;
  }
  .inbox {
    border-right: 0;
  }
  .application-sheet dl {
    grid-template-columns: 1fr 1fr;
  }
  .application-sheet footer {
    align-items: stretch;
    flex-direction: column;
  }
  .application-sheet footer div {
    flex-direction: column;
  }
  .application-sheet input {
    min-width: 0;
  }
}
</style>
