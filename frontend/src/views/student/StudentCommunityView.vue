<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const userStore = useUserStore()
const activeSection = ref('late')
const loading = ref(true)
const failures = ref([])
const data = ref({ late: [], leave: [], visitor: [], votes: [], appeals: [], credit: null })
const studentId = computed(() => userStore.userInfo?.id || '')
const sections = [
  { key: 'late', code: '01', label: '晚归记录', hint: '查看与说明' },
  { key: 'leave', code: '02', label: '离校报备', hint: '申请与审批' },
  { key: 'visitor', code: '03', label: '访客授权', hint: '动态通行码' },
  { key: 'votes', code: '04', label: '房间投票', hint: '寝室共识' },
  { key: 'credit', code: '05', label: '信用与申诉', hint: '分值与复核' }
]
const activeForm = ref(null) // 'late' | 'leave' | 'visitor' | 'votes' | 'appeals' | null
const formLoading = ref(false)
const feedback = ref('')
const expandedId = ref(null)
const roomId = ref(null)
const form = ref({
  reason: '',
  leaveDate: '',
  returnDate: '',
  destination: '',
  visitorName: '',
  visitReason: '',
  visitEnd: '',
  topic: '',
  eligibleCount: 4,
  creditRecordId: ''
})
const deductibleLogs = computed(() => {
  const items = data.value.credit?.items || data.value.credit?.Items || []
  return items.filter((log) => Number(log.scoreChange) < 0)
})
const formTitle = computed(
  () =>
    ({
      late: '补充晚归说明',
      leave: '新建离校报备',
      visitor: '申请访客码',
      votes: '发起房间投票',
      appeals: '发起信用申诉'
    })[activeForm.value] || ''
)
const openForm = (key) => {
  feedback.value = ''
  form.value = {
    reason: '',
    leaveDate: '',
    returnDate: '',
    destination: '',
    visitorName: '',
    visitReason: '',
    visitEnd: '',
    topic: '',
    eligibleCount: 4,
    creditRecordId: ''
  }
  activeForm.value = key
}
const closeForm = () => {
  activeForm.value = null
  formLoading.value = false
  feedback.value = ''
}
const submitForm = async () => {
  const key = activeForm.value
  if (!key || formLoading.value) return
  formLoading.value = true
  feedback.value = ''
  try {
    if (key === 'late') {
      const record = data.value.late[0]
      if (!record?.recordId) throw new Error('暂无可补充说明的晚归记录')
      await studentApi.updateLateEntryReason(record.recordId, form.value.reason)
      feedback.value = '晚归说明已补充'
    } else if (key === 'leave') {
      await studentApi.createLeaveApplication({
        studentId: studentId.value,
        leaveDate: form.value.leaveDate,
        returnDate: form.value.returnDate,
        destination: form.value.destination
      })
      feedback.value = '离校报备已提交，等待审批'
    } else if (key === 'visitor') {
      await studentApi.createVisitorAuthorization({
        visitorName: form.value.visitorName,
        visitReason: form.value.visitReason || null,
        endTime: form.value.visitEnd
      })
      feedback.value = '访客码已申请'
    } else if (key === 'votes') {
      if (!roomId.value) throw new Error('暂无房间信息，无法发起投票')
      await studentApi.createRoomVote({
        roomId: roomId.value,
        topic: form.value.topic,
        eligibleCount: Number(form.value.eligibleCount)
      })
      feedback.value = '投票已发起'
    } else if (key === 'appeals') {
      if (!form.value.creditRecordId) throw new Error('请选择要申诉的扣分明细')
      await studentApi.createCreditAppeal({
        creditRecordId: Number(form.value.creditRecordId),
        reason: form.value.reason
      })
      feedback.value = '申诉已提交'
    }
    await loadCommunity()
    closeForm()
  } catch (requestError) {
    if (key === 'appeals' && requestError?.status === 404) {
      feedback.value = '申诉接口后端未实现（BUG-104），待补后可用'
    } else {
      feedback.value = toUserMessage(requestError, '提交失败，请稍后重试')
    }
  } finally {
    formLoading.value = false
  }
}
const itemKey = (item) =>
  item.recordId || item.applyId || item.authId || item.voteId || item.appealId || item.id
const toggleDetail = (item) => {
  const key = itemKey(item)
  expandedId.value = expandedId.value === key ? null : key
}
const currentItems = computed(() =>
  activeSection.value === 'credit' ? data.value.appeals : data.value[activeSection.value]
)
const activeCopy = computed(() => sections.find((section) => section.key === activeSection.value))

const loadCommunity = async () => {
  loading.value = true
  failures.value = []
  try {
    const accommodation = await studentApi.getAccommodation(studentId.value).catch(() => null)
    const requests = [
      ['late', studentApi.getLateEntries(studentId.value)],
      ['leave', studentApi.getLeaveApplications(studentId.value)],
      ['visitor', studentApi.getVisitorAuthorizations(studentId.value)],
      ['appeals', studentApi.getCreditAppeals(studentId.value)],
      ['credit', studentApi.getCredit(studentId.value)]
    ]
    roomId.value = accommodation?.roomId || null
    if (accommodation?.roomId)
      requests.push(['votes', studentApi.getRoomVotes(accommodation.roomId)])
    const results = await Promise.allSettled(requests.map(([, request]) => request))
    results.forEach((result, index) => {
      const key = requests[index][0]
      if (result.status === 'rejected') failures.value.push(key)
      else if (key === 'credit') data.value.credit = result.value
      else data.value[key] = normalizeCollection(result.value).items
    })
  } finally {
    loading.value = false
  }
}

const itemTitle = (item) =>
  item.title ||
  item.reason ||
  item.destination ||
  item.visitorName ||
  item.voteTitle ||
  item.appealReason ||
  `记录 #${item.recordId || item.applyId || item.authId || item.voteId || item.appealId || '—'}`
const itemMeta = (item) =>
  item.returnTime ||
  item.leaveDate ||
  item.visitTime ||
  item.createdAt ||
  item.createTime ||
  '时间待同步'
const itemStatus = (item) => item.status || item.result || '已记录'
const detailLabels = {
  recordId: '记录编号',
  studentId: '学生编号',
  recordTime: '记录时间',
  applyId: '申请编号',
  authId: '授权编号',
  voteId: '投票编号',
  appealId: '申诉编号',
  leaveDate: '离校日期',
  returnDate: '返校日期',
  destination: '目的地',
  visitorName: '访客姓名',
  visitReason: '来访事由',
  visitTime: '来访时间',
  visitEnd: '授权截止',
  reason: '说明内容',
  status: '处理状态',
  result: '处理结果',
  title: '标题',
  topic: '投票议题',
  eligibleCount: '应参与人数',
  createdAt: '创建时间',
  createTime: '创建时间'
}
const detailLabel = (field) =>
  detailLabels[field] ||
  field
    .replace(/([A-Z])/g, ' $1')
    .replace(/^./, (character) => character.toUpperCase())
    .trim()
const detailValue = (value) => {
  if (typeof value === 'string' && value.includes('T')) return value.replace('T', ' ')
  if (typeof value === 'object' && value !== null) return JSON.stringify(value)
  return String(value)
}
onMounted(loadCommunity)
</script>

<template>
  <div class="community-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / COMMUNITY"
      title="安全与宿舍社区"
      description="安全事务、访客协作和寝室共识分别处理，但共享同一套身份与通知入口。"
    >
      <StatusTag
        :label="
          data.credit
            ? `信用 ${data.credit.score ?? data.credit.creditScore ?? '正常'}`
            : '信用待同步'
        "
        :tone="data.credit ? 'success' : 'warning'"
      />
    </WorkspaceHeader>
    <div class="community-layout">
      <nav class="community-nav" aria-label="安全社区功能">
        <button
          v-for="section in sections"
          :key="section.key"
          :class="{ active: activeSection === section.key }"
          @click="activeSection = section.key"
        >
          <span>{{ section.code }}</span>
          <div>
            <b>{{ section.label }}</b
            ><small>{{ section.hint }}</small>
          </div>
          <i>{{ section.key === 'credit' ? data.appeals.length : data[section.key].length }}</i>
        </button>
      </nav>
      <section class="community-workspace">
        <header>
          <div>
            <span>COMMUNITY / {{ activeCopy.code }}</span>
            <h2>{{ activeCopy.label }}</h2>
            <p>{{ activeCopy.hint }}相关记录集中显示在这里。</p>
          </div>
          <button class="btn btn-primary" @click="openForm(activeSection)">
            {{
              activeSection === 'late'
                ? '补充说明'
                : activeSection === 'leave'
                  ? '新建报备'
                  : activeSection === 'visitor'
                    ? '申请访客码'
                    : activeSection === 'votes'
                      ? '发起投票'
                      : '发起申诉'
            }}
          </button>
        </header>
        <form v-if="activeForm" class="community-form" @submit.prevent="submitForm">
          <h3>{{ formTitle }}</h3>
          <template v-if="activeForm === 'appeals'">
            <p class="appeal-hint">
              当前信用分：
              {{
                data.credit?.currentScore ?? data.credit?.CurrentScore ?? data.credit?.score ?? '—'
              }}
            </p>
            <template v-if="deductibleLogs.length">
              <label>
                选择扣分明细
                <select v-model="form.creditRecordId" required>
                  <option value="" disabled>选择要申诉的扣分明细</option>
                  <option v-for="log in deductibleLogs" :key="log.logId" :value="log.logId">
                    {{ String(log.createTime || '').slice(0, 10) }} · {{ log.scoreChange }} 分 ·
                    {{ log.reason }}
                  </option>
                </select>
              </label>
              <label>
                申诉原因
                <textarea
                  v-model="form.reason"
                  rows="3"
                  maxlength="200"
                  placeholder="说明申诉事由"
                  required
                ></textarea>
              </label>
            </template>
            <p v-else class="appeal-hint appeal-empty">
              当前信用分无扣减记录，暂无可申诉的扣分明细。
            </p>
          </template>
          <label v-else-if="activeForm === 'late'">
            说明内容
            <textarea
              v-model="form.reason"
              rows="3"
              maxlength="200"
              placeholder="填写说明"
              required
            ></textarea>
          </label>
          <template v-else-if="activeForm === 'leave'">
            <label>离校日期 <input v-model="form.leaveDate" type="date" required /></label>
            <label>返校日期 <input v-model="form.returnDate" type="date" required /></label>
            <label
              >目的地
              <input v-model="form.destination" maxlength="200" required placeholder="目的地"
            /></label>
          </template>
          <template v-else-if="activeForm === 'visitor'">
            <label
              >访客姓名
              <input v-model="form.visitorName" maxlength="50" required placeholder="访客姓名"
            /></label>
            <label
              >来访事由 <input v-model="form.visitReason" maxlength="200" placeholder="选填"
            /></label>
            <label>授权截止 <input v-model="form.visitEnd" type="datetime-local" required /></label>
          </template>
          <template v-else-if="activeForm === 'votes'">
            <label
              >投票议题
              <input v-model="form.topic" maxlength="200" required placeholder="发起什么投票"
            /></label>
            <label
              >应参与人数 <input v-model="form.eligibleCount" type="number" min="1" max="99"
            /></label>
          </template>
          <p v-if="feedback" class="form-feedback" role="status">{{ feedback }}</p>
          <div class="form-actions">
            <button type="button" class="btn" @click="closeForm">取消</button>
            <button
              type="submit"
              class="btn btn-primary"
              :disabled="formLoading || (activeForm === 'appeals' && !deductibleLogs.length)"
            >
              {{ formLoading ? '提交中…' : '提交' }}
            </button>
          </div>
        </form>
        <div v-if="activeSection === 'credit' && data.credit" class="credit-summary">
          <div class="credit-summary__score">
            <span>当前信用分</span>
            <strong>{{ data.credit.currentScore ?? data.credit.score ?? '—' }}</strong>
          </div>
          <div class="credit-summary__state">
            <span>账户状态</span>
            <StatusTag
              :label="data.credit.isFrozen ? '已冻结' : '正常'"
              :tone="data.credit.isFrozen ? 'danger' : 'success'"
            />
          </div>
          <div v-if="data.credit.items?.length" class="credit-summary__logs">
            <span>信用变更记录</span>
            <p v-for="log in data.credit.items" :key="log.logId">
              {{ String(log.createTime || '').slice(0, 10) }} ·
              <b :class="{ minus: Number(log.scoreChange) < 0 }">{{ log.scoreChange }} 分</b> ·
              {{ log.reason }}
            </p>
          </div>
          <p v-else class="credit-summary__empty">当前无信用变更记录。</p>
        </div>
        <InlineState
          :loading="loading"
          :error="failures.includes(activeSection) ? `${activeCopy.label}暂时无法同步` : ''"
          :empty="!loading && !currentItems.length"
          :empty-text="`暂无${activeCopy.label}记录`"
        />
        <div class="community-records">
          <article
            v-for="(item, index) in currentItems"
            :key="
              item.recordId || item.applyId || item.authId || item.voteId || item.appealId || index
            "
          >
            <time>0{{ index + 1 }}</time>
            <div>
              <h3>{{ itemTitle(item) }}</h3>
              <p>{{ itemMeta(item) }}</p>
            </div>
            <StatusTag :label="itemStatus(item)" tone="info" size="small" /><button
              type="button"
              @click="toggleDetail(item)"
            >
              查看详情 ↗
            </button>
            <dl v-if="expandedId === itemKey(item)" class="record-detail">
              <template v-for="(value, field) in item" :key="field">
                <div v-if="value !== null && value !== undefined && value !== ''">
                  <dt>{{ detailLabel(field) }}</dt>
                  <dd>{{ detailValue(value) }}</dd>
                </div>
              </template>
            </dl>
          </article>
        </div>
      </section>
      <aside class="community-guide">
        <span>SAFETY / GUIDE</span>
        <h2>当前提醒</h2>
        <div v-if="activeSection === 'late'">
          <b>及时补充原因</b>
          <p>晚归说明会进入宿管核对流程。</p>
        </div>
        <div v-else-if="activeSection === 'leave'">
          <b>确保返校日期准确</b>
          <p>审批结果和逾期提醒将通过通知中心送达。</p>
        </div>
        <div v-else-if="activeSection === 'visitor'">
          <b>通行码仅限本人使用</b>
          <p>授权到期或撤销后二维码立即失效。</p>
        </div>
        <div v-else-if="activeSection === 'votes'">
          <b>仅寝室成员参与</b>
          <p>结果对同一房间内的成员公开。</p>
        </div>
        <div v-else>
          <b>当前信用状态</b>
          <p>分值变更应能追溯到具体业务事件。</p>
        </div>
        <footer><span>异常或紧急情况</span><b>联系一层值班台</b></footer>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.workspace-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.community-layout {
  display: grid;
  grid-template-columns: 270px minmax(0, 1fr) 310px;
  margin-top: 34px;
  gap: 24px;
}
.community-nav,
.community-workspace,
.community-guide {
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.96);
  box-shadow: var(--shadow-soft);
}
.community-nav {
  overflow: hidden;
  display: flex;
  flex-direction: column;
  color: var(--color-text);
}
.community-nav button {
  position: relative;
  display: grid;
  grid-template-columns: 46px minmax(0, 1fr) 38px;
  align-items: center;
  min-height: 116px;
  padding: 22px 20px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  color: var(--color-text);
  text-align: left;
  gap: 16px;
  cursor: pointer;
  transition:
    background 0.18s ease,
    color 0.18s ease;
}
.community-nav button:hover,
.community-nav button.active {
  background: #eef4ff;
}
.community-nav button.active {
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.12);
}
.community-nav button > span {
  display: grid;
  width: 34px;
  height: 34px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font: 14px var(--font-mono);
  font-weight: 900;
}
.community-nav button div {
  display: grid;
  gap: 8px;
}
.community-nav button b {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 950;
  line-height: 1.25;
}
.community-nav button small {
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 700;
}
.community-nav button > i {
  display: grid;
  min-width: 32px;
  height: 32px;
  place-items: center;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface-muted);
  color: var(--color-brand-strong);
  font: 13px var(--font-mono);
  font-weight: 900;
  font-style: normal;
}
.community-workspace > header {
  position: relative;
  display: flex;
  align-items: end;
  justify-content: space-between;
  overflow: hidden;
  min-height: 156px;
  padding: 34px 38px;
  border-bottom: 0;
  background: var(--color-surface);
  gap: 24px;
}
.community-workspace {
  color: var(--color-text);
}
.community-workspace > header::after {
  content: none;
}
.community-workspace header span,
.community-guide > span {
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}
.community-workspace h2,
.community-guide h2 {
  margin: 10px 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: clamp(26px, 2.3vw, 34px);
  font-weight: 950;
  line-height: 1.15;
}
.community-workspace header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 16px;
  font-weight: 600;
  line-height: 1.7;
}
.community-records article {
  display: grid;
  grid-template-columns: 58px minmax(0, 1fr) auto auto;
  align-items: center;
  min-height: 112px;
  margin: 14px 32px;
  padding: 22px 24px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
  color: var(--color-text);
  gap: 20px;
  transition: background 0.18s ease;
}
.credit-summary {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 12px;
  margin: 0 32px 16px;
  padding: 18px 22px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 12px 24px rgba(23, 65, 120, 0.08);
}
.credit-summary__score span,
.credit-summary__state span,
.credit-summary__logs span {
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
}
.credit-summary__score strong {
  margin-top: 6px;
  font: 900 32px var(--font-display);
}
.credit-summary__state {
  display: grid;
  align-content: start;
  gap: 8px;
}
.credit-summary__logs {
  grid-column: 1/-1;
  display: grid;
  gap: 6px;
}
.credit-summary__logs p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
}
.credit-summary__logs b.minus {
  color: var(--color-danger);
}
.credit-summary__empty {
  grid-column: 1/-1;
  margin: 0;
  color: var(--color-text-muted);
  font-size: 13px;
}
.community-records article:hover {
  background: #eef4ff;
}
.community-records time {
  display: grid;
  width: 44px;
  height: 44px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: #eef8ff;
  color: var(--color-brand);
  font: 15px var(--font-mono);
  font-weight: 900;
}
.community-records article div {
  display: grid;
  gap: 8px;
}
.community-records h3 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
  line-height: 1.35;
}
.community-records p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 650;
  line-height: 1.6;
}
.community-records article > button {
  min-height: 38px;
  padding: 0 12px;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-brand);
  font-size: 14px;
  font-weight: 900;
  cursor: pointer;
}
.community-guide {
  position: relative;
  overflow: hidden;
  padding: 32px;
  background:
    linear-gradient(180deg, rgba(6, 36, 80, 0.98), rgba(6, 55, 113, 0.96)), var(--color-ink);
  color: #fff;
}
.community-guide::before {
  position: absolute;
  inset: 0;
  background:
    linear-gradient(90deg, rgba(255, 255, 255, 0.08) 1px, transparent 1px) 0 0 / 48px 48px,
    linear-gradient(180deg, rgba(255, 255, 255, 0.07) 1px, transparent 1px) 0 0 / 48px 48px;
  content: '';
  opacity: 0.35;
}
.community-guide > * {
  position: relative;
  z-index: 1;
}
.community-guide > span {
  color: #80fbef;
}
.community-guide h2 {
  margin-bottom: 42px;
}
.community-guide > div {
  padding: 24px 0;
  border-block: 1px solid rgba(255, 255, 255, 0.2);
}
.community-guide > div b {
  font-family: var(--font-display);
  font-size: 21px;
  font-weight: 950;
}
.community-guide > div p {
  margin: 14px 0 0;
  color: rgba(255, 255, 255, 0.76);
  font-size: 15px;
  font-weight: 600;
  line-height: 1.8;
}
.community-guide footer {
  display: grid;
  margin-top: 32px;
  padding: 22px;
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.12);
  gap: 8px;
}
.community-guide footer span {
  color: rgba(255, 255, 255, 0.66);
  font-size: 14px;
  font-weight: 700;
}
.community-guide footer b {
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 950;
}
@media (max-width: 1000px) {
  .community-layout {
    grid-template-columns: 230px 1fr;
  }
  .community-guide {
    grid-column: 1/-1;
  }
}
@media (max-width: 720px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .community-layout {
    grid-template-columns: 1fr;
  }
  .community-nav {
    overflow-x: auto;
    flex-direction: row;
  }
  .community-nav button {
    min-width: 190px;
  }
  .community-guide {
    grid-column: auto;
  }
  .community-records article {
    grid-template-columns: 58px 1fr;
  }
  .community-records :deep(.status-tag),
  .community-records article > button {
    grid-column: 2;
    justify-self: start;
  }
}
.community-form {
  display: grid;
  gap: 18px;
  margin: 26px;
  padding: 26px;
  border-radius: var(--radius-lg);
  background: #f5f8fd;
}
.community-form h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 950;
}
.community-form label {
  display: grid;
  gap: 9px;
  color: var(--color-text);
  font-size: 15px;
  font-weight: 850;
}
.community-form input,
.community-form textarea,
.community-form select {
  min-height: 48px;
  padding: 12px 14px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-paper);
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 600;
}
.community-form textarea {
  resize: vertical;
}
.form-feedback {
  margin: 0;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 850;
}
.appeal-hint {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 15px;
  line-height: 1.7;
}
.appeal-empty {
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
}
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 12px;
}
.record-detail {
  grid-column: 1/-1;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  margin: 10px 0 0;
  padding: 8px 20px;
  border: 1px solid var(--color-line);
  border-radius: var(--radius-lg);
  background: var(--color-surface-muted);
  gap: 0 28px;
}
.record-detail div {
  display: grid;
  grid-template-columns: minmax(92px, 0.42fr) minmax(0, 1fr);
  align-items: start;
  min-width: 0;
  padding: 12px 0;
  border-bottom: 1px solid var(--color-line);
  gap: 14px;
  font-size: 14px;
  line-height: 1.6;
}
.record-detail dt {
  color: var(--color-text-muted);
  font-weight: 750;
}
.record-detail dd {
  min-width: 0;
  margin: 0;
  color: var(--color-ink);
  font-weight: 750;
  overflow-wrap: anywhere;
}
@media (max-width: 720px) {
  .record-detail {
    grid-column: 1/-1;
    grid-template-columns: 1fr;
    gap: 0;
  }
  .record-detail div {
    grid-template-columns: 96px minmax(0, 1fr);
  }
}
</style>
