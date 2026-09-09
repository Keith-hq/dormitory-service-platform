<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { MetricStrip, WorkspaceHeader } from '@/components'
import { toUserMessage } from '@/utils/errorMessage'

const visitor = ref({ visitorName: '', phone: '', studentId: '' })
const registry = ref({ registryId: '', qrToken: '' })
const latestRecord = ref(null)
const feedback = ref('')
const working = ref('')
const inHouse = ref([])

const metrics = computed(() => [
  { label: '登记编号', value: registry.value.registryId || '—', hint: '本次值守' },
  { label: '通行状态', value: latestRecord.value?.status || '待登记', hint: '扫码核验' },
  { label: '离场时间', value: latestRecord.value?.exitTime ? '已记录' : '—', hint: '闭环状态' }
])

const run = async (key, action, success) => {
  working.value = key
  feedback.value = ''
  try {
    const data = await action()
    latestRecord.value = data
    if (data?.registryId) registry.value.registryId = String(data.registryId)
    if (data?.qrToken) registry.value.qrToken = data.qrToken
    feedback.value = typeof success === 'function' ? success(data) : success
  } catch (error) {
    feedback.value = toUserMessage(error, '门岗操作失败，请核对登记编号与通行码')
  } finally {
    working.value = ''
  }
}

const loadActive = async () => {
  try {
    const data = await adminApi.listVisitors()
    inHouse.value = Array.isArray(data) ? data : Array.isArray(data?.items) ? data.items : []
  } catch {
    inHouse.value = []
  }
}

const submitVisitor = async () => {
  await run(
    'register',
    () =>
      adminApi.registerVisitor({
        visitorName: visitor.value.visitorName,
        phone: visitor.value.phone || null,
        studentId: visitor.value.studentId || null
      }),
    (data) =>
      data?.qrToken
        ? '访客登记已提交，登记编号与通行码已带入核验区'
        : '访客登记已提交，登记编号已带入核验区；请扫描访客二维码获取通行码'
  )
  if (latestRecord.value) visitor.value = { visitorName: '', phone: '', studentId: '' }
  await loadActive()
}

const verifyVisitor = async () => {
  await run(
    'verify',
    () => adminApi.verifyVisitor(registry.value.registryId, { qrToken: registry.value.qrToken }),
    '访客通行码核验成功'
  )
  await loadActive()
}

const recordExitFor = async (registryId) => {
  await run('exit', () => adminApi.recordVisitorExit(registryId), '该访客离场时间已登记')
  await loadActive()
}

onMounted(loadActive)
</script>

<template>
  <main class="duty-page">
    <WorkspaceHeader
      eyebrow="VISITOR DUTY DESK"
      title="访客值守"
      description="登记、扫码核验与离场共用同一编号，让一次来访在门岗完成闭环。"
    >
      <span class="live-dot">值班在线</span>
    </WorkspaceHeader>
    <MetricStrip :metrics="metrics" style="--metric-count: 3" />
    <p v-if="feedback" class="feedback" role="status">{{ feedback }}</p>

    <section class="duty-flow">
      <article class="register-card">
        <header>
          <span>01 / ON-SITE REGISTRATION</span>
          <h2>现场访客登记</h2>
          <p>核对来访人和被访学生后生成门岗记录。</p>
        </header>
        <form @submit.prevent="submitVisitor">
          <label
            >访客姓名<input
              v-model.trim="visitor.visitorName"
              required
              placeholder="请输入真实姓名"
          /></label>
          <label
            >联系电话<input
              v-model.trim="visitor.phone"
              maxlength="20"
              placeholder="可选，用于现场核验"
          /></label>
          <label class="wide"
            >被访学生学号<input
              v-model.trim="visitor.studentId"
              maxlength="20"
              placeholder="可选，如 TST_STU_81501"
          /></label>
          <button class="btn btn-primary" :disabled="working === 'register'">
            {{ working === 'register' ? '登记中…' : '登记并生成编号' }}
          </button>
        </form>
      </article>

      <article class="checkpoint-card">
        <header>
          <span>02 / CHECKPOINT</span>
          <h2>通行核验</h2>
          <p>扫码得到通行码后，与登记编号一起核验。</p>
        </header>
        <form @submit.prevent="verifyVisitor">
          <label
            >登记编号<input
              v-model="registry.registryId"
              required
              min="1"
              type="number"
              placeholder="Registry ID"
          /></label>
          <label
            >二维码通行码<input
              v-model.trim="registry.qrToken"
              required
              placeholder="扫描或粘贴 QR Token"
          /></label>
          <button class="btn" :disabled="working === 'verify'">
            {{ working === 'verify' ? '核验中…' : '核验通行码' }}
          </button>
        </form>
      </article>
    </section>

    <section class="inhouse-card">
      <header>
        <span>03 / EXIT LOG</span>
        <h2>离场登记</h2>
        <p>在场(尚未离场)访客在此列出，逐位点击「记录离场」各自闭环。</p>
      </header>
      <ul v-if="inHouse.length" class="inhouse-list">
        <li v-for="row in inHouse" :key="row.registryId">
          <div>
            <strong>{{ row.visitorName }}</strong>
            <small
              >#{{ row.registryId }} · 被访 {{ row.studentId || '—' }} ·
              {{ (row.enterTime || '').slice?.(0, 16) || '—' }}</small
            >
          </div>
          <b>{{ row.status }}</b>
          <button
            type="button"
            class="btn btn-sm"
            :disabled="working === 'exit'"
            @click="recordExitFor(row.registryId)"
          >
            记录离场
          </button>
        </li>
      </ul>
      <p v-else class="inhouse-empty">当前没有在场访客。</p>
    </section>
  </main>
</template>

<style scoped>
.duty-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.live-dot {
  display: flex;
  align-items: center;
  min-height: 34px;
  padding: 0 12px;
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font: 800 12px var(--font-mono);
  gap: 8px;
}
.live-dot:before {
  content: '';
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 0 4px rgba(53, 90, 72, 0.12);
}
.feedback {
  margin: 18px 0 0;
  padding: 12px 16px;
  background: var(--color-brand-soft);
  border-radius: var(--radius-lg);
  color: var(--color-brand-strong);
  font-size: 13px;
}
.duty-flow {
  display: grid;
  grid-template-columns: 1.08fr 0.92fr;
  gap: 20px;
  margin-top: 28px;
}
.duty-flow article {
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.register-card,
.checkpoint-card {
  min-height: 390px;
  padding: 30px;
}
.register-card {
  background: #fff;
  color: var(--color-text);
}
.checkpoint-card {
  background: #fff;
}
.duty-flow header {
  min-height: 110px;
  margin-bottom: 16px;
}
.register-card header {
  border-color: transparent;
}
.duty-flow header span,
.exit-card span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.duty-flow h2 {
  margin: 10px 0 6px;
  color: var(--color-ink);
  font: 900 28px var(--font-display);
  line-height: 1.2;
}
.duty-flow header p,
.exit-card p {
  color: var(--color-text-muted);
  font-size: 13px;
  line-height: 1.7;
}
.duty-flow form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-top: 18px;
}
.duty-flow label {
  display: flex;
  flex-direction: column;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
  gap: 7px;
}
.duty-flow label.wide,
.duty-flow form button {
  grid-column: 1/-1;
}
.duty-flow input {
  width: 100%;
  min-height: 44px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 14px;
}
.duty-flow input:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}
.checkpoint-card form {
  grid-template-columns: 1fr;
}
.checkpoint-card form button {
  grid-column: auto;
  margin-top: 8px;
}
.exit-card {
  display: flex;
  grid-column: 1/-1;
  align-items: center;
  justify-content: space-between;
  gap: 20px;
  padding: 24px 30px;
  background: #fff;
}
.exit-card h2 {
  font-size: 24px;
}
.exit-card p {
  margin: 0;
}
@media (max-width: 780px) {
  .duty-flow {
    grid-template-columns: 1fr;
  }
  .exit-card {
    grid-column: auto;
    align-items: flex-start;
    flex-direction: column;
    gap: 16px;
  }
  .duty-flow form {
    grid-template-columns: 1fr;
  }
  .duty-flow label.wide,
  .duty-flow form button {
    grid-column: auto;
  }
}
.inhouse-card {
  margin-top: 22px;
  padding: 24px 28px;
  border: 1px solid var(--color-line, #e2e9f3);
  border-radius: var(--radius-lg, 12px);
  background: #fff;
}
.inhouse-card > header span {
  color: var(--color-brand, #1f6feb);
  font-size: 14px;
  font-weight: 850;
}
.inhouse-card h2 {
  margin: 6px 0 4px;
  color: var(--color-ink, #12233f);
  font-family: var(--font-display, inherit);
  font-size: 22px;
}
.inhouse-card p {
  margin: 0 0 8px;
  color: var(--color-text-muted, #5a6b85);
  font-size: 13px;
}
.inhouse-list {
  margin: 8px 0 0;
  padding: 0;
  list-style: none;
}
.inhouse-list li {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 10px 0;
  border-top: 1px solid var(--color-line, #eef2f8);
}
.inhouse-list li > div {
  display: grid;
  min-width: 0;
  flex: 1 1 auto;
}
.inhouse-list strong {
  color: var(--color-ink, #12233f);
}
.inhouse-list small {
  color: var(--color-text-muted, #5a6b85);
  font-size: 12px;
}
.inhouse-list b {
  color: var(--color-brand, #1f6feb);
  font-size: 13px;
}
.inhouse-empty {
  color: var(--color-text-muted, #5a6b85);
  font-size: 14px;
}
</style>
