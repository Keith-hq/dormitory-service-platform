<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const loading = ref(true)
const error = ref('')
const actionLoading = ref('')
const feedback = ref('')
const wallet = ref(null)
const fees = ref([])
const rechargeAmount = ref(100)

const studentId = computed(() => userStore.userInfo?.id || '')
const unpaidFees = computed(() => fees.value.filter((item) => item.isPaid !== '是'))
const unpaidTotal = computed(() =>
  unpaidFees.value.reduce((sum, item) => sum + Number(item.total || 0), 0)
)
const paidTotal = computed(() =>
  fees.value
    .filter((item) => item.isPaid === '是')
    .reduce((sum, item) => sum + Number(item.total || 0), 0)
)

const loadFinance = async () => {
  loading.value = true
  error.value = ''
  const [walletResult, feeResult] = await Promise.allSettled([
    studentApi.getWallet(studentId.value),
    studentApi.getFees(studentId.value)
  ])
  if (walletResult.status === 'fulfilled') wallet.value = walletResult.value
  if (feeResult.status === 'fulfilled') fees.value = normalizeCollection(feeResult.value).items
  if (walletResult.status === 'rejected' && feeResult.status === 'rejected')
    error.value = '钱包与账单暂时无法同步'
  loading.value = false
}

const payFee = async (item) => {
  actionLoading.value = `fee-${item.detailId}`
  feedback.value = ''
  try {
    await studentApi.payFee(item.detailId, crypto.randomUUID())
    feedback.value = '缴费成功，账单状态已更新'
    await loadFinance()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '缴费失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}

const recharge = async () => {
  actionLoading.value = 'recharge'
  feedback.value = ''
  try {
    await studentApi.rechargeWallet(Number(rechargeAmount.value), crypto.randomUUID())
    feedback.value = '充值成功'
    await loadFinance()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '充值失败，请稍后重试')
  } finally {
    actionLoading.value = ''
  }
}

onMounted(loadFinance)
</script>

<template>
  <div class="finance-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / FINANCE"
      title="费用与校园钱包"
      description="每笔费用都能追溯到账期、分摊依据和钱包流水。"
    >
      <StatusTag v-if="wallet?.lowBalanceWarning" label="余额偏低" tone="warning" />
      <StatusTag v-else label="账户正常" tone="success" />
    </WorkspaceHeader>
    <InlineState :loading="loading" :error="error" />

    <template v-if="!loading">
      <section class="wallet-band">
        <div class="balance">
          <span>AVAILABLE BALANCE</span
          ><strong>¥{{ Number(wallet?.balance || 0).toFixed(2) }}</strong
          ><small>低余额提醒线 ¥{{ Number(wallet?.lowBalanceThreshold || 0).toFixed(2) }}</small>
        </div>
        <div class="fee-summary">
          <article>
            <span>待缴合计</span><b>¥{{ unpaidTotal.toFixed(2) }}</b
            ><small>{{ unpaidFees.length }} 笔</small>
          </article>
          <article>
            <span>历史已缴</span><b>¥{{ paidTotal.toFixed(2) }}</b
            ><small>{{ fees.length - unpaidFees.length }} 笔</small>
          </article>
        </div>
        <form @submit.prevent="recharge">
          <label for="recharge-amount">快捷充值</label>
          <div>
            <select id="recharge-amount" v-model.number="rechargeAmount" class="form-select">
              <option :value="50">¥50</option>
              <option :value="100">¥100</option>
              <option :value="200">¥200</option></select
            ><button class="btn btn-primary" :disabled="actionLoading === 'recharge'">
              {{ actionLoading === 'recharge' ? '处理中…' : '充值' }}
            </button>
          </div>
        </form>
      </section>

      <p v-if="feedback" class="finance-feedback" role="status">{{ feedback }}</p>

      <div class="finance-grid">
        <section class="bill-ledger">
          <header>
            <div>
              <span>BILLS / {{ fees.length }}</span>
              <h2>水电分摊账单</h2>
            </div>
            <small>按账期倒序</small>
          </header>
          <InlineState :empty="!fees.length" empty-text="暂无账单记录" />
          <article v-for="item in fees" :key="item.detailId">
            <time>{{ item.yearMonth || '未标记账期' }}</time>
            <div class="bill-split">
              <span
                >水费 <b>¥{{ Number(item.waterShare || 0).toFixed(2) }}</b></span
              ><i></i
              ><span
                >电费 <b>¥{{ Number(item.powerShare || 0).toFixed(2) }}</b></span
              >
            </div>
            <div class="bill-total">
              <span>{{ item.stayDays }}/{{ item.totalDays }} 天</span
              ><strong>¥{{ Number(item.total || 0).toFixed(2) }}</strong>
            </div>
            <StatusTag
              :label="item.isPaid === '是' ? '已缴清' : '待缴费'"
              :tone="item.isPaid === '是' ? 'success' : 'warning'"
              size="small"
            />
            <button
              v-if="item.isPaid !== '是'"
              class="btn btn-sm btn-primary"
              :disabled="actionLoading === `fee-${item.detailId}`"
              @click="payFee(item)"
            >
              {{ actionLoading === `fee-${item.detailId}` ? '支付中…' : '立即缴费' }}
            </button>
          </article>
        </section>

        <aside class="wallet-flow">
          <header>
            <span>WALLET / LOG</span>
            <h2>钱包流水</h2>
          </header>
          <InlineState :empty="!wallet?.logs?.length" empty-text="暂无钱包流水" />
          <article v-for="log in wallet?.logs || []" :key="log.logId">
            <div>
              <b>{{ log.transactionType }}</b
              ><time>{{
                log.createTime ? new Date(log.createTime).toLocaleDateString('zh-CN') : '—'
              }}</time>
            </div>
            <strong :class="{ income: Number(log.amount) > 0 }"
              >{{ Number(log.amount) > 0 ? '+' : '' }}¥{{ Number(log.amount).toFixed(2) }}</strong
            ><small>余额 ¥{{ Number(log.afterBalance).toFixed(2) }}</small>
          </article>
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
.wallet-band {
  display: grid;
  grid-template-columns: 1.1fr 1fr 0.7fr;
  min-height: 150px;
  margin: 27px 0;
  border: 1px solid var(--color-line-strong);
  background: var(--color-ink);
  color: var(--color-paper);
}
.balance,
.fee-summary,
.wallet-band form {
  padding: 23px 25px;
}
.balance {
  display: grid;
  border-right: 1px solid #40534a;
}
.balance > span,
.bill-ledger header span,
.wallet-flow header span {
  color: #df8d70;
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.balance strong {
  margin-top: 12px;
  font: 400 40px var(--font-display);
}
.balance small {
  align-self: end;
  color: #8fa096;
  font-size: 8px;
}
.fee-summary {
  display: grid;
  grid-template-columns: 1fr 1fr;
  border-right: 1px solid #40534a;
  gap: 18px;
}
.fee-summary article {
  display: grid;
  align-content: space-between;
}
.fee-summary span {
  color: #91a198;
  font-size: 9px;
}
.fee-summary b {
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 500;
}
.fee-summary small {
  color: #70837a;
  font-size: 8px;
}
.wallet-band form {
  display: grid;
  align-content: center;
  gap: 9px;
}
.wallet-band form > label {
  color: #91a198;
  font-size: 9px;
}
.wallet-band form > div {
  display: flex;
  gap: 7px;
}
.wallet-band .form-select {
  min-width: 82px;
  border-color: #65766d;
  background: #203229;
  color: #f5edde;
}
.finance-feedback {
  padding: 11px 14px;
  border-left: 3px solid var(--color-accent);
  background: var(--color-surface-muted);
  color: var(--color-brand);
  font-size: 10px;
}
.finance-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(280px, 0.6fr);
  gap: 18px;
}
.bill-ledger,
.wallet-flow {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.5);
}
.bill-ledger > header,
.wallet-flow > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  padding: 17px 20px;
  border-bottom: 1px solid var(--color-line);
}
.bill-ledger h2,
.wallet-flow h2 {
  margin: 5px 0 0;
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 500;
}
.bill-ledger header small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.bill-ledger > article {
  display: grid;
  grid-template-columns: 90px 1fr 90px auto auto;
  align-items: center;
  min-height: 80px;
  padding: 13px 20px;
  border-bottom: 1px solid var(--color-line);
  gap: 15px;
}
.bill-ledger time {
  color: var(--color-accent-strong);
  font: 9px var(--font-mono);
}
.bill-split {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--color-text-muted);
  font-size: 9px;
}
.bill-split i {
  width: 1px;
  height: 20px;
  background: var(--color-line);
}
.bill-split b {
  display: block;
  margin-top: 4px;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 12px;
}
.bill-total {
  display: grid;
  gap: 4px;
}
.bill-total span {
  color: var(--color-text-soft);
  font-size: 8px;
}
.bill-total strong {
  font-family: var(--font-display);
  font-size: 15px;
}
.wallet-flow > article {
  display: grid;
  grid-template-columns: 1fr auto;
  min-height: 76px;
  align-items: center;
  padding: 13px 18px;
  border-bottom: 1px solid var(--color-line);
  gap: 5px;
}
.wallet-flow article div {
  display: grid;
  gap: 4px;
}
.wallet-flow article b {
  font-family: var(--font-display);
  font-size: 12px;
}
.wallet-flow time,
.wallet-flow small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.wallet-flow article > strong {
  color: var(--color-danger);
  font-family: var(--font-mono);
  font-size: 10px;
}
.wallet-flow article > strong.income {
  color: var(--color-brand);
}
.wallet-flow article > small {
  grid-column: 2;
  text-align: right;
}
@media (max-width: 900px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .wallet-band,
  .finance-grid {
    grid-template-columns: 1fr;
  }
  .balance,
  .fee-summary {
    border-right: 0;
    border-bottom: 1px solid #40534a;
  }
  .bill-ledger > article {
    grid-template-columns: 1fr 1fr;
  }
  .bill-split {
    grid-column: 1/-1;
  }
  .wallet-band form > div {
    flex-wrap: wrap;
  }
}
</style>
