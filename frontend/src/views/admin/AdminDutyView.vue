<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { InlineState, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const active = ref('access')
const loading = ref(true)
const error = ref('')
const accessLogs = ref([])
const violations = ref([])
const tabs = [
  { key: 'access', label: '门禁流' },
  { key: 'violations', label: '违规台账' },
  { key: 'visitor', label: '访客登记' }
]
const visitor = ref({ visitorName: '', phone: '', studentId: '', visitReason: '' })
const feedback = ref('')
const submitting = ref(false)
const currentRows = computed(() =>
  active.value === 'access' ? accessLogs.value : violations.value
)

const load = async () => {
  loading.value = true
  error.value = ''
  const [access, violation] = await Promise.allSettled([
    adminApi.getAccessLogs({ page: 1, pageSize: 30 }),
    adminApi.getViolations({ page: 1, pageSize: 30 })
  ])
  if (access.status === 'fulfilled') accessLogs.value = normalizeCollection(access.value).items
  if (violation.status === 'fulfilled')
    violations.value = normalizeCollection(violation.value).items
  if (access.status === 'rejected' && violation.status === 'rejected')
    error.value = toUserMessage(access.reason, '安全数据暂时无法同步')
  loading.value = false
}
const submitVisitor = async () => {
  submitting.value = true
  feedback.value = ''
  try {
    await adminApi.registerVisitor(visitor.value)
    feedback.value = '访客登记已提交'
    visitor.value = { visitorName: '', phone: '', studentId: '', visitReason: '' }
  } catch (e) {
    feedback.value = toUserMessage(e, '访客登记失败')
  } finally {
    submitting.value = false
  }
}
const valueOf = (row, ...keys) =>
  keys
    .map((key) => row[key])
    .find((value) => value !== undefined && value !== null && value !== '') ?? '—'
onMounted(load)
</script>

<template>
  <main class="duty-page">
    <WorkspaceHeader
      eyebrow="SAFETY DUTY DESK"
      title="安全值班"
      description="门禁异常按时间流阅读，违规按台账核对，访客在现场直接登记。"
    >
      <span class="live-dot">值班在线</span>
    </WorkspaceHeader>
    <nav class="ledger-tabs">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        :class="{ active: active === tab.key }"
        @click="active = tab.key"
      >
        {{ tab.label
        }}<b v-if="tab.key !== 'visitor'">{{
          tab.key === 'access' ? accessLogs.length : violations.length
        }}</b>
      </button>
    </nav>
    <InlineState :loading="loading" :error="error" />

    <section v-if="!loading && active !== 'visitor'" class="event-ledger">
      <header>
        <span>TIME</span><span>PERSON / LOCATION</span><span>EVENT</span><span>STATUS</span>
      </header>
      <article v-for="(row, index) in currentRows" :key="row.logId ?? row.violationId ?? index">
        <time>{{ valueOf(row, 'accessTime', 'occurredAt', 'vioDate') }}</time>
        <div>
          <strong>{{ valueOf(row, 'studentName', 'studentId') }}</strong
          ><small>{{ valueOf(row, 'roomName', 'buildingName') }}</small>
        </div>
        <p>{{ valueOf(row, 'direction', 'type', 'vioType', 'description') }}</p>
        <b>{{ valueOf(row, 'status', 'processStatus') }}</b>
      </article>
      <p v-if="!currentRows.length" class="empty">当前分类没有记录。</p>
    </section>

    <section v-else-if="active === 'visitor'" class="visitor-desk">
      <div class="visitor-copy">
        <span>ON-SITE REGISTRATION</span>
        <h2>现场访客登记</h2>
        <p>核对来访人身份和被访学生后提交。后续扫码核验与离场记录沿用同一登记编号。</p>
      </div>
      <form @submit.prevent="submitVisitor">
        <label
          >访客姓名<input v-model.trim="visitor.visitorName" required placeholder="请输入真实姓名"
        /></label>
        <label
          >联系电话<input v-model.trim="visitor.phone" required placeholder="用于现场核验"
        /></label>
        <label
          >被访学生学号<input v-model.trim="visitor.studentId" required placeholder="如 20260001"
        /></label>
        <label class="wide"
          >来访事由<textarea
            v-model.trim="visitor.visitReason"
            required
            rows="3"
            placeholder="简要说明来访事项"
          ></textarea>
        </label>
        <p v-if="feedback" class="feedback">{{ feedback }}</p>
        <button class="btn btn-primary" :disabled="submitting">
          {{ submitting ? '提交中…' : '登记访客' }}
        </button>
      </form>
    </section>
  </main>
</template>

<style scoped>
.duty-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.live-dot {
  display: flex;
  align-items: center;
  gap: 8px;
  font: 10px var(--font-mono);
  color: var(--color-success);
}
.live-dot:before {
  content: '';
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
}
.ledger-tabs {
  display: flex;
  border-bottom: 1px solid var(--color-line-strong);
  margin: 28px 0 20px;
}
.ledger-tabs button {
  display: flex;
  align-items: center;
  gap: 9px;
  padding: 13px 20px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: transparent;
  color: var(--color-text-muted);
  font: 600 11px var(--font-body);
  cursor: pointer;
}
.ledger-tabs button.active {
  border-color: var(--color-accent-strong);
  color: var(--color-ink);
}
.ledger-tabs b {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border-radius: 20px;
  background: var(--color-canvas-deep);
  font: 9px var(--font-mono);
}
.event-ledger {
  border-top: 1px solid var(--color-line-strong);
}
.event-ledger header,
.event-ledger article {
  display: grid;
  grid-template-columns: 170px 1.1fr 1.4fr 100px;
  gap: 18px;
  align-items: center;
  padding: 13px 16px;
  border-bottom: 1px solid var(--color-line);
}
.event-ledger header {
  color: var(--color-text-soft);
  font: 8px var(--font-mono);
  letter-spacing: 0.12em;
}
.event-ledger article:hover {
  background: rgba(255, 255, 255, 0.5);
}
.event-ledger time {
  font: 10px var(--font-mono);
  color: var(--color-text-muted);
}
.event-ledger div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.event-ledger strong {
  font-size: 12px;
}
.event-ledger small,
.event-ledger p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 10px;
}
.event-ledger article > b {
  font-size: 10px;
  color: var(--color-accent-strong);
}
.empty {
  padding: 28px;
  color: var(--color-text-muted);
}
.visitor-desk {
  display: grid;
  grid-template-columns: 0.7fr 1.3fr;
  gap: 48px;
  padding: 38px;
  background: var(--color-ink);
  color: #fff;
}
.visitor-copy span {
  color: var(--color-accent);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.visitor-copy h2 {
  margin: 12px 0;
  font: 500 30px var(--font-display);
}
.visitor-copy p {
  color: #aaa39a;
  font-size: 11px;
  line-height: 1.8;
}
.visitor-desk form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 18px;
}
.visitor-desk label {
  display: flex;
  flex-direction: column;
  gap: 8px;
  font-size: 10px;
  color: #c7c0b6;
}
.visitor-desk .wide {
  grid-column: 1/-1;
}
.visitor-desk input,
.visitor-desk textarea {
  border: 1px solid #4c4944;
  background: #252422;
  color: #fff;
  padding: 12px;
  font: 12px var(--font-body);
}
.feedback {
  margin: 0;
  color: var(--color-accent);
  font-size: 11px;
}
@media (max-width: 760px) {
  .event-ledger header {
    display: none;
  }
  .event-ledger article {
    grid-template-columns: 1fr;
  }
  .visitor-desk {
    grid-template-columns: 1fr;
    padding: 25px;
  }
  .visitor-desk form {
    grid-template-columns: 1fr;
  }
  .visitor-desk .wide {
    grid-column: auto;
  }
}
</style>
