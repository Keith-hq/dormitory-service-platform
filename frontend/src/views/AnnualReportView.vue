<script setup>
import { computed, ref, watch } from 'vue'
import { reportApi } from '@/api/report'
import { MetricDial, PageHeader, StatusTag } from '@/components'
import { useUserStore } from '@/store/user'
import {
  ANNUAL_REPORT_REFERENCES,
  getDaysRepresented,
  getMetricProgress,
  normalizeAnnualReport
} from '@/utils/annualReport'

const userStore = useUserStore()
const currentYear = new Date().getFullYear()
const selectedYear = ref(currentYear)
const report = ref(normalizeAnnualReport(null, currentYear))
const loading = ref(true)
const errorMessage = ref('')
const mockEnabled = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true'
const studentId = computed(() => userStore.userInfo?.id || '')
let latestLoadId = 0

const yearOptions = Array.from({ length: 3 }, (_, index) => currentYear - index)

const daysRepresented = computed(() => getDaysRepresented(report.value.year))
const averageDailyAccess = computed(() => {
  if (!daysRepresented.value) return 0
  return Math.round((report.value.accessCount / daysRepresented.value) * 10) / 10
})

const metricDials = computed(() => [
  {
    key: 'utility',
    label: '水电支出',
    value: report.value.utilityTotal.toFixed(1),
    suffix: '元',
    progress: getMetricProgress(report.value.utilityTotal, ANNUAL_REPORT_REFERENCES.utilityTotal),
    caption: `参考环上限 ¥${ANNUAL_REPORT_REFERENCES.utilityTotal}`,
    tone: 'amber'
  },
  {
    key: 'hygiene',
    label: '卫生均分',
    value: report.value.hygieneAvg.toFixed(1),
    suffix: '分',
    progress: getMetricProgress(report.value.hygieneAvg, ANNUAL_REPORT_REFERENCES.hygieneAvg),
    caption: '按年度卫生检查记录汇总',
    tone: 'jade'
  },
  {
    key: 'access',
    label: '门禁通行',
    value: report.value.accessCount,
    suffix: '次',
    progress: getMetricProgress(report.value.accessCount, ANNUAL_REPORT_REFERENCES.accessCount),
    caption: `日均约 ${averageDailyAccess.value} 次通行`,
    tone: 'blue'
  }
])

const insightCards = computed(() => [
  {
    number: '01',
    title: '共同生活的成本',
    value: `¥${report.value.utilityTotal.toFixed(1)}`,
    description: '这是本年度归集到个人的水电支出总额，帮助你回看宿舍资源使用情况。'
  },
  {
    number: '02',
    title: '被认真维护的空间',
    value: `${report.value.hygieneAvg.toFixed(1)} / 100`,
    description: '卫生均分来自年度检查记录，它记录的不只是整洁，也是一间宿舍的共同习惯。'
  },
  {
    number: '03',
    title: '往返校园的轨迹',
    value: `${report.value.accessCount} 次`,
    description: `在报告覆盖的 ${daysRepresented.value} 天里，日均约有 ${averageDailyAccess.value} 次门禁通行。`
  }
])

const loadReport = async () => {
  const loadId = ++latestLoadId
  const requestedStudentId = studentId.value
  if (!requestedStudentId) {
    loading.value = false
    return
  }

  loading.value = true
  errorMessage.value = ''
  try {
    const data = await reportApi.getAnnual(requestedStudentId, selectedYear.value)
    if (loadId !== latestLoadId) return
    report.value = normalizeAnnualReport(data, selectedYear.value)
  } catch (error) {
    if (loadId !== latestLoadId) return
    if (error?.code === 401 || error?.status === 401) return
    errorMessage.value = error?.message || '年度报告加载失败'
  } finally {
    if (loadId === latestLoadId) loading.value = false
  }
}

const printReport = () => window.print()

watch([selectedYear, studentId], () => loadReport(), { immediate: true })
</script>

<template>
  <div class="annual-page">
    <PageHeader
      eyebrow="ANNUAL LIVING REVIEW"
      title="年度生活报告"
      description="把水电支出、卫生表现与门禁轨迹收拢成一页，回看这一年真实而具体的宿舍生活。"
    >
      <StatusTag v-if="mockEnabled" label="契约 Mock" tone="warning" />
      <label class="year-picker">
        <span>报告年份</span>
        <select v-model.number="selectedYear" class="form-select" :disabled="loading">
          <option v-for="year in yearOptions" :key="year" :value="year">{{ year }}</option>
        </select>
      </label>
      <button
        type="button"
        class="btn"
        :disabled="loading || Boolean(errorMessage)"
        @click="printReport"
      >
        打印报告
      </button>
    </PageHeader>

    <div v-if="errorMessage" class="report-error" role="alert">
      <div>
        <strong>报告暂时无法抵达</strong>
        <p>{{ errorMessage }}</p>
      </div>
      <button type="button" class="btn" @click="loadReport">重新加载</button>
    </div>

    <template v-else>
      <section class="report-cover" :aria-busy="loading">
        <div class="report-cover__year" aria-hidden="true">
          <span>{{ report.year }}</span>
          <small>MY DORMITORY YEAR</small>
        </div>

        <div class="report-cover__story">
          <span class="story-kicker">生活不是统计，但统计会留下生活的形状</span>
          <h2>{{ userStore.userName || '同学' }}的宿舍年度</h2>
          <p v-if="loading" class="story-loading" role="status">正在汇集这一年的生活片段…</p>
          <p v-else>{{ report.overview || '这一年的宿舍生活数据已经整理完成。' }}</p>
          <footer>
            <span>REPORT ID</span>
            <strong>{{ report.year }}—{{ studentId || '—' }}</strong>
          </footer>
        </div>
      </section>

      <section class="metric-gallery" aria-labelledby="metric-heading">
        <header>
          <span>THREE MEASURES</span>
          <h2 id="metric-heading">这一年的三个刻度</h2>
          <p>圆环用于呈现当前值相对参考上限的位置；精确数值以接口返回为准。</p>
        </header>
        <div class="metric-gallery__grid">
          <MetricDial v-for="metric in metricDials" :key="metric.key" v-bind="metric" />
        </div>
      </section>

      <section class="insight-section" aria-labelledby="insight-heading">
        <header class="insight-heading">
          <div>
            <span>EDITORIAL NOTES</span>
            <h2 id="insight-heading">数据背后的生活注脚</h2>
          </div>
          <p>{{ report.year }} 年 · REP-03</p>
        </header>

        <div class="insight-grid">
          <article v-for="card in insightCards" :key="card.number" class="insight-card">
            <span>{{ card.number }}</span>
            <div>
              <h3>{{ card.title }}</h3>
              <strong>{{ card.value }}</strong>
              <p>{{ card.description }}</p>
            </div>
          </article>
        </div>
      </section>

      <aside class="method-note">
        <span class="method-note__mark" aria-hidden="true">注</span>
        <div>
          <strong>数据口径</strong>
          <p>
            本页仅使用 Apifox REP-03 返回的年度水电合计、卫生平均分、门禁通行次数与年度概述。
            圆环参考上限只用于视觉定位，不代表平台评分或业务阈值。
          </p>
        </div>
      </aside>
    </template>
  </div>
</template>

<style scoped>
.annual-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 80px;
}

.year-picker {
  display: grid;
  gap: 4px;
  text-align: left;
}

.year-picker span {
  color: var(--color-text-soft);
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.1em;
}

.year-picker .form-select {
  min-width: 108px;
}

.report-error {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-5);
  margin-top: var(--space-7);
  padding: var(--space-5);
  border: 1px solid #efc4bc;
  border-radius: var(--radius-md);
  background: var(--color-danger-soft);
}

.report-error strong {
  color: var(--color-danger);
}

.report-error p {
  margin: 5px 0 0;
  color: var(--color-text-muted);
  font-size: 13px;
}

.report-cover {
  display: grid;
  grid-template-columns: minmax(270px, 0.82fr) minmax(0, 1.18fr);
  overflow: hidden;
  min-height: 420px;
  margin-top: var(--space-7);
  border-radius: 3px 30px 3px 30px;
  background: #18383c;
  color: #fff;
  box-shadow: var(--shadow-lift);
}

.report-cover__year {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: flex-end;
  overflow: hidden;
  padding: clamp(30px, 5vw, 58px);
  background:
    radial-gradient(circle at 18% 18%, rgba(238, 192, 105, 0.24), transparent 27%),
    linear-gradient(145deg, #b97236, #d7a254 60%, #eccb86);
}

.report-cover__year::after {
  content: '';
  position: absolute;
  top: -110px;
  left: -100px;
  width: 340px;
  height: 340px;
  border: 1px solid rgba(255, 255, 255, 0.22);
  border-radius: 50%;
  box-shadow:
    0 0 0 48px rgba(255, 255, 255, 0.055),
    0 0 0 96px rgba(255, 255, 255, 0.028);
}

.report-cover__year span {
  position: relative;
  z-index: 1;
  font-family: var(--font-display);
  font-size: clamp(74px, 10vw, 132px);
  font-weight: 700;
  letter-spacing: -0.08em;
  line-height: 0.82;
}

.report-cover__year small {
  position: relative;
  z-index: 1;
  margin-top: var(--space-5);
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.2em;
}

.report-cover__story {
  position: relative;
  display: flex;
  flex-direction: column;
  justify-content: center;
  padding: clamp(34px, 6vw, 72px);
  background:
    linear-gradient(rgba(255, 255, 255, 0.035) 1px, transparent 1px),
    linear-gradient(90deg, rgba(255, 255, 255, 0.035) 1px, transparent 1px);
  background-size: 34px 34px;
}

.story-kicker {
  color: #e6bd75;
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.12em;
}

.report-cover__story h2 {
  max-width: 560px;
  margin: var(--space-4) 0 var(--space-5);
  color: #fffdf8;
  font-family: var(--font-display);
  font-size: clamp(34px, 5vw, 56px);
  letter-spacing: -0.04em;
  line-height: 1.13;
}

.report-cover__story > p {
  max-width: 610px;
  min-height: 76px;
  margin: 0;
  color: rgba(255, 255, 255, 0.7);
  font-size: 15px;
  line-height: 1.9;
}

.story-loading {
  animation: loading-pulse 1.2s ease-in-out infinite alternate;
}

.report-cover__story footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  margin-top: var(--space-7);
  padding-top: var(--space-4);
  border-top: 1px solid rgba(255, 255, 255, 0.12);
}

.report-cover__story footer span {
  color: rgba(255, 255, 255, 0.4);
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.15em;
}

.report-cover__story footer strong {
  color: rgba(255, 255, 255, 0.68);
  font-family: var(--font-mono);
  font-size: 10px;
  font-weight: 500;
}

.metric-gallery {
  display: grid;
  grid-template-columns: 0.72fr 1.8fr;
  gap: clamp(32px, 6vw, 76px);
  margin-top: 64px;
  padding: clamp(30px, 5vw, 54px);
  border-radius: var(--radius-lg);
  background: #18383c;
}

.metric-gallery > header > span,
.insight-heading span {
  color: #dcb46e;
  font-size: 9px;
  font-weight: 800;
  letter-spacing: 0.18em;
}

.metric-gallery h2 {
  margin: var(--space-3) 0 var(--space-4);
  color: #fffdf8;
  font-family: var(--font-display);
  font-size: clamp(25px, 3vw, 34px);
  line-height: 1.25;
}

.metric-gallery > header p {
  margin: 0;
  color: rgba(255, 255, 255, 0.5);
  font-size: 12px;
  line-height: 1.8;
}

.metric-gallery__grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--space-5);
}

.insight-section {
  margin-top: 64px;
}

.insight-heading {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: var(--space-5);
  margin-bottom: var(--space-5);
}

.insight-heading span {
  color: var(--color-brand-strong);
}

.insight-heading h2 {
  margin: 7px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 28px;
}

.insight-heading > p {
  margin: 0;
  color: var(--color-text-soft);
  font-family: var(--font-mono);
  font-size: 10px;
}

.insight-grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  border-top: 1px solid var(--color-line-strong);
  border-bottom: 1px solid var(--color-line-strong);
}

.insight-card {
  min-height: 300px;
  padding: var(--space-6);
  border-right: 1px solid var(--color-line-strong);
}

.insight-card:last-child {
  border-right: 0;
}

.insight-card > span {
  color: var(--color-accent);
  font-family: var(--font-mono);
  font-size: 10px;
}

.insight-card > div {
  display: flex;
  height: calc(100% - 26px);
  flex-direction: column;
  justify-content: flex-end;
}

.insight-card h3 {
  margin: 0 0 var(--space-3);
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
}

.insight-card strong {
  color: var(--color-brand-strong);
  font-family: var(--font-display);
  font-size: clamp(28px, 4vw, 44px);
  font-variant-numeric: tabular-nums;
}

.insight-card p {
  margin: var(--space-4) 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.8;
}

.method-note {
  display: flex;
  align-items: flex-start;
  gap: var(--space-4);
  margin-top: var(--space-7);
  padding: var(--space-5);
  border: 1px dashed var(--color-brand-border);
  border-radius: var(--radius-md);
  background: rgba(220, 238, 232, 0.38);
}

.method-note__mark {
  display: grid;
  width: 36px;
  height: 36px;
  flex: 0 0 auto;
  place-items: center;
  border-radius: 10px 3px 10px 3px;
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-family: var(--font-display);
  font-weight: 700;
}

.method-note strong {
  color: var(--color-ink);
  font-size: 13px;
}

.method-note p {
  margin: 5px 0 0;
  color: var(--color-text-muted);
  font-size: 12px;
  line-height: 1.75;
}

@keyframes loading-pulse {
  from {
    opacity: 0.45;
  }
  to {
    opacity: 0.9;
  }
}

@media (max-width: 980px) {
  .metric-gallery {
    grid-template-columns: 1fr;
  }

  .metric-gallery__grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 720px) {
  .annual-page {
    width: min(100% - 24px, var(--content-max));
  }

  .report-cover {
    grid-template-columns: 1fr;
  }

  .report-cover__year {
    min-height: 230px;
  }

  .metric-gallery__grid,
  .insight-grid {
    grid-template-columns: 1fr;
  }

  .insight-card {
    min-height: 240px;
    border-right: 0;
    border-bottom: 1px solid var(--color-line-strong);
  }

  .insight-card:last-child {
    border-bottom: 0;
  }
}

@media print {
  .annual-page {
    width: 100%;
    padding: 0;
  }

  .year-picker,
  .annual-page :deep(.page-header__aside .btn),
  .annual-page :deep(.status-tag) {
    display: none;
  }

  .report-cover,
  .metric-gallery {
    break-inside: avoid;
    box-shadow: none;
    print-color-adjust: exact;
  }
}
</style>
