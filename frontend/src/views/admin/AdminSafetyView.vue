<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'
import { formatLocalDateTimeInput, formatLocalMonthInput } from '@/utils/localDate'

const now = new Date()
const currentMonth = formatLocalMonthInput(now)
const localDateTime = formatLocalDateTimeInput(now)
const month = ref(currentMonth)
const rankings = ref([])
const loading = ref(true)
const working = ref('')
const error = ref('')
const feedback = ref('')
const hygiene = ref({ roomId: '', score: '', comment: '' })
const correction = ref({ recordId: '', score: '', comment: '' })
const lateEntry = ref({ studentId: '', recordTime: localDateTime, reason: '' })

const average = computed(() => {
  if (!rankings.value.length) return '—'
  const sum = rankings.value.reduce((total, item) => total + Number(item.averageScore || 0), 0)
  return (sum / rankings.value.length).toFixed(1)
})
const metrics = computed(() => [
  { label: '参评房间', value: rankings.value.length, hint: month.value },
  { label: '月均分', value: average.value, hint: '卫生巡检' },
  {
    label: '标杆房间',
    value: rankings.value[0]?.roomId ? `#${rankings.value[0].roomId}` : '—',
    hint: '榜首'
  }
])

const loadRankings = async () => {
  loading.value = true
  error.value = ''
  try {
    const data = await adminApi.getHygieneRankings({ yearMonth: month.value })
    rankings.value = normalizeCollection(data).items.sort((a, b) => Number(a.rank) - Number(b.rank))
  } catch (e) {
    error.value = toUserMessage(e, '卫生月榜暂时无法同步')
  } finally {
    loading.value = false
  }
}

const run = async (key, action, success) => {
  working.value = key
  feedback.value = ''
  try {
    await action()
    feedback.value = success
    return true
  } catch (e) {
    feedback.value = toUserMessage(e, '登记失败，请核对输入后重试')
    return false
  } finally {
    working.value = ''
  }
}

const createHygiene = async () => {
  const ok = await run(
    'hygiene',
    () =>
      adminApi.createHygieneRecord({
        roomId: Number(hygiene.value.roomId),
        score: Number(hygiene.value.score),
        comment: hygiene.value.comment || null
      }),
    '卫生评分已登记'
  )
  if (ok) {
    hygiene.value = { roomId: '', score: '', comment: '' }
    await loadRankings()
  }
}

const updateHygiene = async () => {
  const ok = await run(
    'correction',
    () =>
      adminApi.updateHygieneRecord(correction.value.recordId, {
        score: Number(correction.value.score),
        comment: correction.value.comment || null
      }),
    '卫生记录已修正'
  )
  if (ok) await loadRankings()
}

const createLateEntry = async () => {
  const ok = await run(
    'late',
    () =>
      adminApi.createLateEntry({
        studentId: lateEntry.value.studentId,
        recordTime: lateEntry.value.recordTime,
        reason: lateEntry.value.reason || null
      }),
    '晚归记录已登记，学生端可查看并补充原因'
  )
  if (ok) lateEntry.value = { studentId: '', recordTime: localDateTime, reason: '' }
}

onMounted(loadRankings)
</script>

<template>
  <main class="safety-page">
    <WorkspaceHeader
      eyebrow="RESIDENCE INSPECTION"
      title="卫生与晚归"
      description="卫生评分按月形成可复核榜单，晚归事件在值班现场即时登记；暂缓业务与日常巡检解耦。"
    >
      <label class="month-control"
        >巡检月份<input v-model="month" type="month" @change="loadRankings"
      /></label>
    </WorkspaceHeader>
    <MetricStrip :metrics="metrics" style="--metric-count: 3" />
    <InlineState :loading="loading" :error="error" />
    <p v-if="feedback" class="feedback">{{ feedback }}</p>

    <section v-if="!loading" class="inspection-layout">
      <article class="ranking-board">
        <header>
          <div>
            <span>01 / HYGIENE RANKING</span>
            <h2>{{ month }} 卫生月榜</h2>
          </div>
          <small>按月均分排序</small>
        </header>
        <div class="ranking-list">
          <article v-for="(item, index) in rankings" :key="item.roomId">
            <b>{{ String(item.rank ?? index + 1).padStart(2, '0') }}</b>
            <div>
              <strong>房间 {{ item.roomId }}</strong
              ><small>月度平均</small>
            </div>
            <em>{{ Number(item.averageScore || 0).toFixed(1) }}</em>
            <i :style="{ '--score': `${Math.min(100, Number(item.averageScore || 0))}%` }"></i>
          </article>
          <p v-if="!rankings.length">当前月份暂无卫生评分，完成一次巡检后会出现在这里。</p>
        </div>
      </article>

      <aside class="inspection-desk">
        <section class="score-card">
          <header>
            <span>02 / SCORE ENTRY</span>
            <h2>巡检打分</h2>
          </header>
          <form @submit.prevent="createHygiene">
            <label>房间 ID<input v-model="hygiene.roomId" required min="1" type="number" /></label>
            <label
              >得分<input v-model="hygiene.score" required min="0" max="100" step="1" type="number"
            /></label>
            <label class="wide"
              >巡检备注<textarea
                v-model.trim="hygiene.comment"
                rows="3"
                placeholder="记录卫生问题或表扬事项"
              ></textarea>
            </label>
            <button class="btn btn-primary" :disabled="working === 'hygiene'">提交评分</button>
          </form>
          <details>
            <summary>修正已有记录</summary>
            <form @submit.prevent="updateHygiene">
              <label
                >记录 ID<input v-model="correction.recordId" required min="1" type="number"
              /></label>
              <label
                >修正分数<input
                  v-model="correction.score"
                  required
                  min="0"
                  max="100"
                  step="1"
                  type="number"
              /></label>
              <label class="wide"
                >修正说明<textarea v-model.trim="correction.comment" rows="2"></textarea>
              </label>
              <button class="btn" :disabled="working === 'correction'">保存修正</button>
            </form>
          </details>
        </section>

        <section class="late-card">
          <header>
            <span>03 / LATE ENTRY</span>
            <h2>晚归登记</h2>
          </header>
          <form @submit.prevent="createLateEntry">
            <label
              >学生学号<input
                v-model.trim="lateEntry.studentId"
                required
                placeholder="输入学生学号"
            /></label>
            <label
              >记录时间<input v-model="lateEntry.recordTime" required type="datetime-local"
            /></label>
            <label
              >现场说明<textarea
                v-model.trim="lateEntry.reason"
                rows="3"
                placeholder="可选；学生后续可补充原因"
              ></textarea>
            </label>
            <button class="btn" :disabled="working === 'late'">登记晚归</button>
          </form>
        </section>
      </aside>
    </section>

    <section class="deferred-note">
      <span>04 / DEFERRED SCOPE</span>
      <div>
        <h2>违规业务暂缓</h2>
        <p>
          VIOL-01 / VIOL-02 的数据字段与楼栋筛选口径尚未锁定，当前不发起 501
          接口请求；卫生评分和晚归登记可独立正常使用。
        </p>
      </div>
      <b>NO BLOCKING CALLS</b>
    </section>
  </main>
</template>

<style scoped>
.safety-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.month-control {
  display: flex;
  align-items: center;
  gap: 10px;
  color: var(--color-text-muted);
  font-size: 9px;
}
.month-control input {
  padding: 9px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  font-size: 11px;
}
.feedback {
  margin: 16px 0 0;
  padding: 11px 15px;
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 11px;
}
.inspection-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(340px, 0.75fr);
  gap: 24px;
  margin-top: 28px;
}
.ranking-board {
  border: 1px solid var(--color-line-strong);
  background: var(--color-ink);
  color: #fff;
}
.ranking-board > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 28px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.17);
}
.ranking-board header span,
.inspection-desk header span,
.deferred-note > span {
  color: var(--color-accent);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.ranking-board h2,
.inspection-desk h2,
.deferred-note h2 {
  margin: 8px 0 0;
  font: 500 27px var(--font-display);
}
.ranking-board header small {
  color: #aaa39a;
  font-size: 9px;
}
.ranking-list article {
  display: grid;
  grid-template-columns: 45px 1fr 52px;
  gap: 14px;
  align-items: center;
  position: relative;
  padding: 19px 28px;
  border-bottom: 1px solid rgba(255, 255, 255, 0.11);
  overflow: hidden;
}
.ranking-list article > b {
  color: var(--color-accent);
  font: 10px var(--font-mono);
}
.ranking-list article div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.ranking-list strong {
  font-size: 12px;
}
.ranking-list small {
  color: #8f918c;
  font-size: 8px;
}
.ranking-list em {
  font: 500 24px var(--font-display);
  font-style: normal;
  text-align: right;
}
.ranking-list i {
  position: absolute;
  bottom: 0;
  left: 0;
  width: var(--score);
  height: 2px;
  background: var(--color-accent);
}
.ranking-list > p {
  padding: 28px;
  color: #aaa39a;
  font-size: 10px;
}
.inspection-desk {
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.inspection-desk section {
  padding: 24px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.24);
}
.inspection-desk h2 {
  color: var(--color-ink);
  font-size: 23px;
}
.inspection-desk form {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin-top: 18px;
}
.inspection-desk label {
  display: flex;
  flex-direction: column;
  gap: 6px;
  color: var(--color-text-muted);
  font-size: 9px;
}
.inspection-desk label.wide,
.inspection-desk form > button {
  grid-column: 1/-1;
}
.inspection-desk input,
.inspection-desk textarea {
  width: 100%;
  padding: 9px 10px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface);
  font-size: 11px;
}
.score-card details {
  margin-top: 16px;
  padding-top: 14px;
  border-top: 1px solid var(--color-line);
}
.score-card summary {
  cursor: pointer;
  color: var(--color-accent-strong);
  font-size: 9px;
}
.late-card form {
  grid-template-columns: 1fr;
}
.late-card form > button {
  grid-column: auto;
}
.deferred-note {
  display: grid;
  grid-template-columns: 160px 1fr auto;
  gap: 24px;
  align-items: center;
  margin-top: 24px;
  padding: 24px 28px;
  border: 1px dashed #d3a79c;
  background: var(--color-danger-soft);
}
.deferred-note h2 {
  font-size: 20px;
}
.deferred-note p {
  max-width: 700px;
  margin: 7px 0 0;
  color: var(--color-danger);
  font-size: 10px;
  line-height: 1.7;
}
.deferred-note > b {
  color: var(--color-danger);
  font: 8px var(--font-mono);
  letter-spacing: 0.1em;
}
@media (max-width: 900px) {
  .inspection-layout {
    grid-template-columns: 1fr;
  }
  .inspection-desk {
    display: grid;
    grid-template-columns: 1fr 1fr;
  }
  .deferred-note {
    grid-template-columns: 1fr;
  }
  .deferred-note > b {
    justify-self: start;
  }
}
@media (max-width: 650px) {
  .inspection-desk {
    grid-template-columns: 1fr;
  }
  .inspection-desk form {
    grid-template-columns: 1fr;
  }
  .inspection-desk label.wide,
  .inspection-desk form > button {
    grid-column: auto;
  }
  .ranking-board > header {
    align-items: start;
    flex-direction: column;
    gap: 8px;
  }
}
</style>
