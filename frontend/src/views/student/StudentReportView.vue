<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const year = ref(new Date().getFullYear())
const loading = ref(true)
const error = ref('')
const annual = ref(null)
const monthlyFee = ref(null)
const facilityUsage = ref(null)
const studentId = computed(() => userStore.userInfo?.id || '')
const metrics = computed(() => [
  {
    label: '年度水电支出',
    value: `¥${Number(annual.value?.utilityTotal || 0).toFixed(2)}`,
    note: monthlyFee.value?.yearMonth || '全年汇总'
  },
  { label: '卫生平均分', value: Number(annual.value?.hygieneAvg || 0).toFixed(1), note: '百分制' },
  { label: '门禁出入次数', value: String(annual.value?.accessCount || 0), note: '年度累计' },
  {
    label: '设施使用次数',
    value: String(facilityUsage.value?.usageCount || 0),
    note: facilityUsage.value?.yearMonth || '本月'
  }
])
const loadReport = async () => {
  loading.value = true
  error.value = ''
  const [annualResult, feeResult, usageResult] = await Promise.allSettled([
    studentApi.getAnnualReport(studentId.value, { year: year.value }),
    studentApi.getMonthlyFeeReport(studentId.value),
    studentApi.getFacilityUsageReport(studentId.value)
  ])
  if (annualResult.status === 'fulfilled') annual.value = annualResult.value
  if (feeResult.status === 'fulfilled') monthlyFee.value = feeResult.value
  if (usageResult.status === 'fulfilled') facilityUsage.value = usageResult.value
  if (annualResult.status === 'rejected')
    error.value = toUserMessage(annualResult.reason, '年度报告暂时无法生成')
  loading.value = false
}
onMounted(loadReport)
</script>

<template>
  <div class="report-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / ANNUAL REPORT"
      :title="`${year} 宿舍生活报告`"
      description="把费用、卫生、门禁和设施使用汇总成一份可追溯的年度回顾。"
    >
      <select v-model.number="year" class="form-select" @change="loadReport">
        <option v-for="item in [2026, 2025, 2024]" :key="item" :value="item">{{ item }} 年</option>
      </select>
    </WorkspaceHeader>
    <InlineState :loading="loading" :error="error" />
    <template v-if="!loading && annual">
      <section class="report-cover">
        <div>
          <span>PERSONAL EDITION / {{ studentId }}</span>
          <h2>{{ annual.overview || '这一年的住校生活，正在形成你的校园记忆。' }}</h2>
          <p>{{ userStore.userName }} · {{ userStore.userInfo?.buildingName || '宿舍园区' }}</p>
        </div>
        <strong>{{ year }}</strong>
      </section>
      <section class="report-metrics">
        <article v-for="(metric, index) in metrics" :key="metric.label">
          <span>0{{ index + 1 }} / {{ metric.label }}</span
          ><strong>{{ metric.value }}</strong
          ><small>{{ metric.note }}</small>
        </article>
      </section>
      <div class="report-grid">
        <section class="expense-figure">
          <header>
            <span>UTILITY / MONTHLY</span>
            <h2>费用观察</h2>
          </header>
          <div class="expense-amount">
            <span>最近账期</span
            ><strong>¥{{ Number(monthlyFee?.utilityTotal || 0).toFixed(2) }}</strong
            ><small>已缴 ¥{{ Number(monthlyFee?.paidTotal || 0).toFixed(2) }}</small>
          </div>
          <div class="expense-bars">
            <i
              v-for="(height, index) in [34, 48, 42, 58, 51, 69, 63, 74, 67, 82, 76, 88]"
              :key="index"
              :style="{ height: `${height}%` }"
              ><small>{{ index + 1 }}</small></i
            >
          </div>
        </section>
        <aside class="habit-note">
          <span>LIVING NOTE</span>
          <h2>生活注解</h2>
          <p>
            报告只使用与你相关的住宿数据。门禁频率用于个人回顾，不代表纪律评价；具体扣分以信用流水和违规记录为准。
          </p>
          <dl>
            <div>
              <dt>数据范围</dt>
              <dd>{{ year }}.01—{{ year }}.12</dd>
            </div>
            <div>
              <dt>更新时间</dt>
              <dd>刚刚同步</dd>
            </div>
            <div>
              <dt>报告版本</dt>
              <dd>PERSONAL / 01</dd>
            </div>
          </dl>
        </aside>
      </div>
    </template>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.report-cover {
  display: flex;
  min-height: 240px;
  align-items: end;
  justify-content: space-between;
  margin: 27px 0 0;
  padding: 35px 38px;
  background: #b9ced0;
  color: #213d32;
  overflow: hidden;
}
.report-cover > div > span,
.expense-figure header span,
.habit-note > span {
  font: 8px var(--font-mono);
  letter-spacing: 0.16em;
}
.report-cover h2 {
  max-width: 720px;
  margin: 18px 0 12px;
  font-family: var(--font-display);
  font-size: clamp(28px, 3.4vw, 47px);
  font-weight: 500;
  line-height: 1.2;
}
.report-cover p {
  margin: 0;
  color: #597369;
  font-size: 10px;
}
.report-cover > strong {
  color: rgba(33, 61, 50, 0.18);
  font: 500 clamp(70px, 12vw, 160px) var(--font-display);
  line-height: 0.75;
}
.report-metrics {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  border: 1px solid var(--color-line-strong);
  border-top: 0;
}
.report-metrics article {
  display: grid;
  min-height: 118px;
  padding: 18px 20px;
  border-right: 1px solid var(--color-line);
  align-content: space-between;
}
.report-metrics article:last-child {
  border-right: 0;
}
.report-metrics span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.report-metrics strong {
  font-family: var(--font-display);
  font-size: 28px;
  font-weight: 500;
}
.report-metrics small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.report-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.4fr) minmax(260px, 0.55fr);
  margin-top: 18px;
  gap: 18px;
}
.expense-figure,
.habit-note {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.52);
}
.expense-figure {
  display: grid;
  grid-template-columns: 180px 1fr;
  grid-template-rows: auto 1fr;
  min-height: 310px;
}
.expense-figure > header {
  grid-column: 1/-1;
  padding: 18px 21px;
  border-bottom: 1px solid var(--color-line);
}
.expense-figure h2,
.habit-note h2 {
  margin: 6px 0 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 500;
}
.expense-amount {
  display: grid;
  align-content: center;
  padding: 24px;
  border-right: 1px solid var(--color-line);
}
.expense-amount span,
.expense-amount small {
  color: var(--color-text-muted);
  font-size: 9px;
}
.expense-amount strong {
  margin: 12px 0;
  font: 500 29px var(--font-display);
}
.expense-bars {
  display: flex;
  height: 220px;
  align-items: end;
  padding: 28px 25px 30px;
  gap: 10px;
}
.expense-bars i {
  position: relative;
  flex: 1;
  min-width: 8px;
  background: var(--color-brand);
}
.expense-bars i:nth-child(3n) {
  background: var(--color-accent);
}
.expense-bars small {
  position: absolute;
  right: 0;
  bottom: -18px;
  left: 0;
  color: var(--color-text-soft);
  font: 7px var(--font-mono);
  text-align: center;
}
.habit-note {
  padding: 24px;
  background: var(--color-ink);
  color: var(--color-paper);
}
.habit-note > span {
  color: #df8e70;
}
.habit-note p {
  margin: 24px 0;
  color: #9bad9f;
  font-size: 10px;
  line-height: 1.9;
}
.habit-note dl {
  margin: 0;
  border-top: 1px solid #42564b;
}
.habit-note dl div {
  display: flex;
  justify-content: space-between;
  padding: 14px 0;
  border-bottom: 1px solid #394e42;
}
.habit-note dt {
  color: #819489;
  font-size: 8px;
}
.habit-note dd {
  margin: 0;
  font: 8px var(--font-mono);
}
@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .report-metrics {
    grid-template-columns: 1fr 1fr;
  }
  .report-grid {
    grid-template-columns: 1fr;
  }
  .report-cover > strong {
    display: none;
  }
}
@media (max-width: 560px) {
  .report-metrics {
    grid-template-columns: 1fr;
  }
  .expense-figure {
    grid-template-columns: 1fr;
  }
  .expense-amount {
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
}
</style>
