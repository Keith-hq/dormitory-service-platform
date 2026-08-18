<script setup>
import { computed, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { MetricStrip, WorkspaceHeader } from '@/components'
import { toUserMessage } from '@/utils/errorMessage'

const visitor = ref({ visitorName: '', phone: '', studentId: '' })
const registry = ref({ registryId: '', qrToken: '' })
const latestRecord = ref(null)
const feedback = ref('')
const working = ref('')

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
    feedback.value = success
  } catch (error) {
    feedback.value = toUserMessage(error, '门岗操作失败，请核对登记编号与通行码')
  } finally {
    working.value = ''
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
    '访客登记已提交，登记编号与通行码已带入核验区'
  )
  if (latestRecord.value) visitor.value = { visitorName: '', phone: '', studentId: '' }
}

const verifyVisitor = () =>
  run(
    'verify',
    () => adminApi.verifyVisitor(registry.value.registryId, { qrToken: registry.value.qrToken }),
    '访客通行码核验成功'
  )

const recordExit = () =>
  run(
    'exit',
    () => adminApi.recordVisitorExit(registry.value.registryId),
    '访客离场时间已登记，本次来访闭环完成'
  )
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

      <article class="exit-card">
        <div>
          <span>03 / EXIT LOG</span>
          <h2>离场登记</h2>
          <p>访客离开时记录离场时间，关闭本次来访。</p>
        </div>
        <button
          class="btn"
          :disabled="!registry.registryId || working === 'exit'"
          @click="recordExit"
        >
          {{ working === 'exit' ? '记录中…' : '确认访客离场' }}
        </button>
      </article>
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
  color: var(--color-brand);
  font: 10px var(--font-mono);
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
  border-left: 3px solid var(--color-accent);
  background: var(--color-brand-soft);
  font-size: 11px;
}
.duty-flow {
  display: grid;
  grid-template-columns: 1.08fr 0.92fr;
  gap: 20px;
  margin-top: 28px;
}
.duty-flow article {
  border: 1px solid var(--color-line-strong);
}
.register-card,
.checkpoint-card {
  min-height: 390px;
  padding: 30px;
}
.register-card {
  background: var(--color-ink);
  color: #fff;
}
.checkpoint-card {
  background: rgba(255, 255, 255, 0.28);
}
.duty-flow header {
  min-height: 110px;
  border-bottom: 1px solid var(--color-line);
}
.register-card header {
  border-color: rgba(255, 255, 255, 0.15);
}
.duty-flow header span,
.exit-card span {
  color: var(--color-accent);
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.duty-flow h2 {
  margin: 10px 0 6px;
  font: 500 28px var(--font-display);
}
.duty-flow header p,
.exit-card p {
  color: var(--color-text-muted);
  font-size: 10px;
  line-height: 1.7;
}
.register-card header p {
  color: #9aa49e;
}
.duty-flow form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 15px;
  margin-top: 24px;
}
.duty-flow label {
  display: flex;
  flex-direction: column;
  color: var(--color-text-muted);
  font-size: 9px;
  gap: 7px;
}
.register-card label {
  color: #b2bbb5;
}
.duty-flow label.wide,
.duty-flow form button {
  grid-column: 1/-1;
}
.duty-flow input {
  width: 100%;
  min-height: 42px;
  padding: 10px 12px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 11px;
}
.register-card input {
  border-color: #46534c;
  background: #233129;
  color: #fff;
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
  padding: 24px 30px;
  background: var(--color-surface-muted);
}
.exit-card h2 {
  font-size: 22px;
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
</style>
