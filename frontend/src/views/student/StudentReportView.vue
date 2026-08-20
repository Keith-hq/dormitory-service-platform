<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const loading = ref(true)
const error = ref('')
const monthlyFee = ref(null)
const facilityUsage = ref(null)
const studentId = computed(() => userStore.userInfo?.id || '')
const period = computed(
  () => monthlyFee.value?.yearMonth || facilityUsage.value?.yearMonth || '当前账期'
)
const outstanding = computed(() =>
  Math.max(
    Number(monthlyFee.value?.utilityTotal || 0) - Number(monthlyFee.value?.paidTotal || 0),
    0
  )
)
const metrics = computed(() => [
  {
    label: '本期水电',
    value: `¥${Number(monthlyFee.value?.utilityTotal || 0).toFixed(2)}`,
    hint: period.value
  },
  {
    label: '已缴金额',
    value: `¥${Number(monthlyFee.value?.paidTotal || 0).toFixed(2)}`,
    hint: '账单实缴'
  },
  {
    label: '待缴金额',
    value: `¥${outstanding.value.toFixed(2)}`,
    hint: outstanding.value > 0 ? '请及时处理' : '本期已结清'
  },
  {
    label: '设施使用',
    value: String(facilityUsage.value?.usageCount || 0),
    hint: `${period.value} 次数`
  }
])

const loadReport = async () => {
  loading.value = true
  error.value = ''
  const [feeResult, usageResult] = await Promise.allSettled([
    studentApi.getMonthlyFeeReport(studentId.value),
    studentApi.getFacilityUsageReport(studentId.value)
  ])

  monthlyFee.value = feeResult.status === 'fulfilled' ? feeResult.value : null
  facilityUsage.value = usageResult.status === 'fulfilled' ? usageResult.value : null

  if (feeResult.status === 'rejected' && usageResult.status === 'rejected') {
    error.value = toUserMessage(feeResult.reason, '生活统计暂时无法同步')
  }
  loading.value = false
}

onMounted(loadReport)
</script>

<template>
  <main class="snapshot-page">
    <WorkspaceHeader
      eyebrow="STUDENT / MONTHLY SNAPSHOT"
      title="本月生活统计"
      description="只保留已经接入真实数据的费用与设施使用指标，帮助你快速确认本期状态。"
    >
      <button class="btn btn-sm" type="button" :disabled="loading" @click="loadReport">
        重新同步
      </button>
    </WorkspaceHeader>

    <InlineState :loading="loading" :error="error" />

    <template v-if="!loading && !error">
      <MetricStrip :metrics="metrics" style="--metric-count: 4" />

      <section class="snapshot-grid">
        <article class="finance-card">
          <header>
            <span>01 / UTILITY STATUS</span>
            <small>{{ period }}</small>
          </header>
          <div class="amount-row">
            <div>
              <small>本期费用</small>
              <strong>¥{{ Number(monthlyFee?.utilityTotal || 0).toFixed(2) }}</strong>
            </div>
            <div>
              <small>已缴金额</small>
              <strong>¥{{ Number(monthlyFee?.paidTotal || 0).toFixed(2) }}</strong>
            </div>
          </div>
          <div class="settlement" :class="{ due: outstanding > 0 }">
            <span>{{ outstanding > 0 ? '仍有待缴费用' : '本期费用已结清' }}</span>
            <b>¥{{ outstanding.toFixed(2) }}</b>
          </div>
        </article>

        <aside class="facility-card">
          <span>02 / FACILITY USE</span>
          <strong>{{ facilityUsage?.usageCount || 0 }}</strong>
          <h2>本期设施使用次数</h2>
          <p>统计已确认的设施使用记录。预约详情与后续操作仍在“设施共享”页面完成。</p>
          <router-link to="/student/facilities">查看设施共享 →</router-link>
        </aside>
      </section>

      <footer class="scope-note">
        <span>DATA SCOPE</span>
        <p>
          当前仅展示已完成真实联调的月度费用与设施使用数据；年度聚合报告属于暂缓业务，不纳入本轮交付。
        </p>
      </footer>
    </template>
  </main>
</template>

<style scoped>
.snapshot-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.snapshot-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(260px, 0.65fr);
  gap: 20px;
  margin-top: 30px;
}
.finance-card,
.facility-card {
  border: 0;
  border-radius: var(--radius-lg);
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}
.finance-card {
  background: #fff;
}
.finance-card header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 30px 34px 12px;
  border-bottom: 0;
}
.finance-card header span,
.facility-card > span,
.scope-note > span {
  color: var(--color-brand);
  font: inherit;
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.finance-card header small {
  color: var(--color-text-muted);
  font: inherit;
  font-size: 15px;
  font-weight: 700;
}
.amount-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
}
.amount-row > div {
  display: grid;
  min-height: 150px;
  align-content: center;
  padding: 28px;
  margin: 18px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
}
.amount-row small {
  color: var(--color-text-muted);
  font-size: 15px;
}
.amount-row strong {
  margin-top: 12px;
  color: var(--color-ink);
  font: 500 clamp(31px, 4vw, 48px) var(--font-display);
}
.settlement {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0 34px 30px;
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 800;
}
.settlement.due {
  color: var(--color-brand);
}
.settlement b {
  font: 500 18px var(--font-display);
}
.facility-card {
  padding: 28px;
  background: #fff;
  color: var(--color-text);
}
.facility-card > strong {
  display: block;
  margin: 32px 0 6px;
  color: var(--color-brand);
  font: 500 76px var(--font-display);
  line-height: 0.9;
}
.facility-card h2 {
  margin: 0;
  font: 500 20px var(--font-display);
}
.facility-card p {
  margin: 22px 0;
  color: var(--color-text-muted);
  font-size: 15px;
  line-height: 1.8;
}
.facility-card a {
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 700;
}
.scope-note {
  display: grid;
  grid-template-columns: 120px 1fr;
  gap: 24px;
  margin-top: 24px;
  padding-top: 20px;
  border-top: 1px solid var(--color-line-strong);
}
.scope-note p {
  max-width: 720px;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 10px;
  line-height: 1.8;
}
@media (max-width: 780px) {
  .snapshot-page {
    width: min(100% - 32px, var(--content-max));
  }
  .snapshot-grid,
  .amount-row {
    grid-template-columns: 1fr;
  }
  .amount-row > div {
    min-height: 110px;
    border-right: 0;
    border-bottom: 1px solid var(--color-line);
  }
  .scope-note {
    grid-template-columns: 1fr;
    gap: 8px;
  }
}
</style>
