<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'
import { formatLocalMonthInput } from '@/utils/localDate'

const userStore = useUserStore()
const activeSection = ref('late')
const loading = ref(true)
const failures = ref([])
const data = ref({
  late: [],
  leave: [],
  visitor: [],
  votes: [],
  appeals: [],
  hygiene: [],
  credit: null
})
const studentId = computed(() => userStore.userInfo?.id || '')
const currentMonth = formatLocalMonthInput()
const hygieneMonth = ref(currentMonth)
const sections = [
  { key: 'late', code: '01', label: '晚归记录', hint: '查看与说明' },
  { key: 'leave', code: '02', label: '离校报备', hint: '申请与审批' },
  { key: 'visitor', code: '03', label: '访客授权', hint: '动态通行码' },
  { key: 'votes', code: '04', label: '房间投票', hint: '寝室共识' },
  { key: 'hygiene', code: '05', label: '卫生排名', hint: '月度月榜' },
  { key: 'credit', code: '06', label: '信用与申诉', hint: '分值与复核' }
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
const appealedLogIds = computed(
  () => new Set((data.value.appeals || []).map((item) => item.creditRecordId))
)
const deductibleLogs = computed(() => {
  const items = data.value.credit?.items || data.value.credit?.Items || []
  return items.filter(
    (log) => Number(log.scoreChange) < 0 && !appealedLogIds.value.has(log.logId)
  )
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
const lateTarget = ref(null) // 正在补充说明的晚归记录
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
const openLateForm = (target) => {
  lateTarget.value = target ?? null
  openForm('late')
}
const openHeaderForm = () => {
  if (activeSection.value === 'late') {
    openLateForm(data.value.late.find((item) => !item.reason) ?? data.value.late[0] ?? null)
    return
  }
  openForm(activeSection.value === 'credit' ? 'appeals' : activeSection.value)
}
const closeForm = () => {
  activeForm.value = null
  formLoading.value = false
  feedback.value = ''
}
const switchSection = (key) => {
  activeSection.value = key
  closeForm()
  expandedId.value = null
}
const submitForm = async () => {
  const key = activeForm.value
  if (!key || formLoading.value) return
  formLoading.value = true
  feedback.value = ''
  try {
    if (key === 'late') {
      const record = lateTarget.value ?? data.value.late[0]
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
    feedback.value = toUserMessage(requestError, '提交失败，请稍后重试')
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

// —— C8 房间投票响应（一人一票）——
const voteStats = ref({})
const votedVoteIds = ref(new Set())
const votingVoteId = ref(null)
const voteStatusTone = (status) =>
  ({ 进行中: 'info', 已通过: 'success', 未通过: 'danger', 已结束: 'warning' })[status] || 'info'
const formatDate = (value) => (value ? new Date(value).toLocaleDateString('zh-CN') : '—')

const submitVote = async (vote, choice) => {
  if (votingVoteId.value) return
  votingVoteId.value = vote.voteId
  feedback.value = ''
  try {
    const stats = await studentApi.submitVoteResponse(vote.voteId, { choice })
    voteStats.value = { ...voteStats.value, [vote.voteId]: stats }
    votedVoteIds.value = new Set([...votedVoteIds.value, vote.voteId])
    feedback.value = `已投「${choice}」`
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '投票失败，可能已投过')
  } finally {
    votingVoteId.value = null
  }
}

const viewVoteStats = async (vote) => {
  try {
    const stats = await studentApi.getRoomVoteStats(vote.voteId)
    voteStats.value = { ...voteStats.value, [vote.voteId]: stats }
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '统计获取失败')
  }
}

// —— C8 访客授权撤销 ——
const revokingId = ref(null)
const revokeVisitor = async (item) => {
  const authId = item.authorizationId ?? item.authId
  if (!authId || revokingId.value) return
  if (!window.confirm(`确认撤销访客「${item.visitorName || ''}」的授权？撤销后通行码立即失效。`))
    return
  revokingId.value = authId
  feedback.value = ''
  try {
    await studentApi.revokeVisitorAuthorization(authId)
    feedback.value = '访客授权已撤销'
    await loadCommunity()
  } catch (requestError) {
    feedback.value = toUserMessage(requestError, '撤销失败')
  } finally {
    revokingId.value = null
  }
}

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
    await loadHygiene()
    await loadMyRoomHygiene()
  } finally {
    loading.value = false
  }
}

const loadHygiene = async () => {
  try {
    const result = await studentApi.getHygieneRankings({ yearMonth: hygieneMonth.value })
    data.value.hygiene = normalizeCollection(result).items
  } catch {
    failures.value.push('hygiene')
  }
}

// 我的宿舍卫生成绩（STU-18：学生只能查自己房间，后端越权 403）
const myRoomRecords = ref([])
const myRoomExpanded = ref(false)
const myRoomLatest = computed(() => myRoomRecords.value[0] ?? null)
const loadMyRoomHygiene = async () => {
  if (!roomId.value) return
  try {
    const result = await studentApi.getRoomHygieneRecords(roomId.value)
    myRoomRecords.value = normalizeCollection(result).items
  } catch {
    // 自己房间卫生成绩获取失败不阻断页面
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
  item.recordTime ||
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
          @click="switchSection(section.key)"
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
          <button
            v-if="activeSection !== 'hygiene'"
            class="btn btn-primary"
            @click="openHeaderForm"
          >
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
          <button v-else class="btn btn-primary" :disabled="loading" @click="loadCommunity">
            刷新卫生排名
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
        <template v-if="activeSection === 'votes'">
          <div class="vote-list">
            <article v-for="vote in data.votes" :key="vote.voteId" class="vote-card">
              <time>{{ formatDate(vote.createTime) }}</time>
              <div class="vote-main">
                <div class="vote-head">
                  <h3>{{ vote.topic }}</h3>
                  <StatusTag :label="vote.status" :tone="voteStatusTone(vote.status)" size="small" />
                </div>
                <p>
                  发起人 {{ vote.initiatorStudentId }} · 应参与 {{ vote.eligibleCount }} 人 ·
                  截止 {{ formatDate(vote.deadline) }}
                </p>
                <div v-if="voteStats[vote.voteId]" class="vote-stats">
                  <span>同意 <b>{{ voteStats[vote.voteId].agreeCount }}</b></span>
                  <span>不同意 <b>{{ voteStats[vote.voteId].disagreeCount }}</b></span>
                  <span>已投 <b>{{ voteStats[vote.voteId].totalCount }}/{{ vote.eligibleCount }}</b></span>
                </div>
              </div>
              <div class="vote-actions">
                <button
                  v-if="vote.status === '进行中'"
                  type="button"
                  class="vote-btn agree"
                  :disabled="votingVoteId === vote.voteId || votedVoteIds.has(vote.voteId)"
                  @click="submitVote(vote, '同意')"
                >
                  同意
                </button>
                <button
                  v-if="vote.status === '进行中'"
                  type="button"
                  class="vote-btn reject"
                  :disabled="votingVoteId === vote.voteId || votedVoteIds.has(vote.voteId)"
                  @click="submitVote(vote, '不同意')"
                >
                  不同意
                </button>
                <button type="button" class="vote-btn view" @click="viewVoteStats(vote)">
                  查看统计
                </button>
                <span v-if="votedVoteIds.has(vote.voteId)" class="voted-hint">✓ 已投票</span>
              </div>
            </article>
          </div>
        </template>

        <template v-else-if="activeSection === 'hygiene'">
          <div class="my-room-card">
            <div class="my-room-head">
              <div>
                <span>MY ROOM / SCORE</span>
                <h3>我的宿舍 · 房间 {{ roomId }}</h3>
              </div>
              <div class="my-room-actions">
                <StatusTag
                  :label="
                    myRoomLatest ? `最新 ${Number(myRoomLatest.score).toFixed(0)} 分` : '暂无评分'
                  "
                  :tone="
                    myRoomLatest && Number(myRoomLatest.score) >= 90
                      ? 'success'
                      : myRoomLatest
                        ? 'warning'
                        : 'info'
                  "
                  size="small"
                />
                <button
                  type="button"
                  class="my-room-toggle"
                  @click="myRoomExpanded = !myRoomExpanded"
                >
                  {{ myRoomExpanded ? '收起记录' : `查看评分记录 (${myRoomRecords.length})` }}
                </button>
              </div>
            </div>
            <div v-if="myRoomExpanded" class="my-room-records">
              <article v-for="record in myRoomRecords" :key="record.recordId">
                <time>{{ formatDate(record.checkDate) }}</time>
                <div>
                  <strong>{{ Number(record.score).toFixed(0) }} 分</strong>
                  <small
                    >{{ record.comment || '无备注'
                    }}{{ record.inspectorId ? ` · 检查人 ${record.inspectorId}` : '' }}</small
                  >
                </div>
              </article>
              <p v-if="!myRoomRecords.length" class="my-room-empty">本宿舍暂无卫生评分记录</p>
            </div>
          </div>
          <div class="hygiene-board">
            <div class="hygiene-header">
              <div>
                <span>HYGIENE / MONTHLY</span>
                <h3>{{ hygieneMonth }} 卫生月榜</h3>
              </div>
              <label class="hygiene-month"
                >月份<input v-model="hygieneMonth" type="month" @change="loadHygiene"
              /></label>
            </div>
            <div class="hygiene-list">
              <article v-for="(item, index) in data.hygiene" :key="item.roomId">
                <b>{{ String(item.rank ?? index + 1).padStart(2, '0') }}</b>
                <div>
                  <strong>房间 {{ item.roomId }}</strong
                  ><small>月度平均</small>
                </div>
                <em>{{ Number(item.averageScore || 0).toFixed(1) }}</em>
                <i :style="{ '--score': `${Math.min(100, Number(item.averageScore || 0))}%` }"></i>
              </article>
              <p v-if="!data.hygiene.length" class="hygiene-empty">
                当前月份暂无卫生评分记录。
              </p>
            </div>
          </div>
        </template>

        <div v-else class="community-records">
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
            <button
              v-if="activeSection === 'late' && !item.reason"
              type="button"
              class="late-btn"
              @click="openLateForm(item)"
            >
              补充说明
            </button>
            <button
              v-if="activeSection === 'visitor' && item.status === '有效'"
              type="button"
              class="revoke-btn"
              :disabled="revokingId === (item.authorizationId ?? item.authId)"
              @click="revokeVisitor(item)"
            >
              {{ revokingId === (item.authorizationId ?? item.authId) ? '撤销中…' : '撤销' }}
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
.vote-list {
  display: grid;
  gap: 14px;
  margin: 14px 32px;
}
.vote-card {
  display: grid;
  grid-template-columns: 110px minmax(0, 1fr) auto;
  align-items: center;
  gap: 22px;
  min-width: 0;
  min-height: 108px;
  padding: 22px 26px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 12px 24px rgba(23, 65, 120, 0.08);
  transition:
    background 0.18s ease,
    box-shadow 0.18s ease,
    transform 0.18s ease;
}
.vote-card:hover {
  background: var(--color-brand-soft);
  box-shadow: 0 16px 32px rgba(11, 99, 199, 0.12);
}
.vote-card > time {
  display: grid;
  min-height: 62px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: #edf5ff;
  color: var(--color-brand-strong);
  font: 15px var(--font-mono);
  font-weight: 950;
  text-align: center;
}
.vote-main {
  min-width: 0;
}
.vote-head {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.vote-card h3 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
  line-height: 1.35;
  overflow-wrap: anywhere;
}
.vote-main > p {
  margin: 8px 0 0;
  color: var(--color-text-muted);
  font-size: 14px;
  font-weight: 650;
  line-height: 1.6;
}
.vote-stats {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
  margin-top: 12px;
}
.vote-stats span {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  min-height: 30px;
  padding: 5px 12px;
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 800;
}
.vote-stats b {
  color: var(--color-brand-strong);
  font-family: var(--font-display);
  font-size: 16px;
  font-weight: 900;
}
.vote-actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 8px;
  flex-wrap: wrap;
}
.vote-btn {
  min-height: 38px;
  padding: 8px 16px;
  border: 1px solid var(--color-line-strong);
  border-radius: 999px;
  background: #fff;
  color: var(--color-text-muted);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 850;
  cursor: pointer;
  transition:
    transform 0.18s ease,
    box-shadow 0.18s ease;
}
.vote-btn:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 8px 18px rgba(11, 99, 199, 0.08);
}
.vote-btn.agree {
  border-color: #b4e6e1;
  background: #e4f8f6;
  color: #007c73;
}
.vote-btn.reject {
  border-color: #efc4bc;
  background: var(--color-danger-soft);
  color: #a34239;
}
.vote-btn.view {
  border-color: var(--color-brand-border);
  background: var(--color-brand-soft);
  color: var(--color-brand);
}
.vote-btn:disabled {
  cursor: not-allowed;
  opacity: 0.45;
  transform: none;
  box-shadow: none;
}
.voted-hint {
  color: var(--color-brand);
  font-size: 13px;
  font-weight: 850;
}
.hygiene-board {
  margin: 14px 32px;
}
.hygiene-header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 16px;
  padding: 0 4px 16px;
}
.hygiene-header span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
}
.hygiene-header h3 {
  margin: 6px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 26px;
  font-weight: 950;
}
.hygiene-header small {
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}
.hygiene-month {
  display: flex;
  align-items: center;
  gap: 8px;
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 700;
}
.hygiene-month input {
  min-height: 38px;
  padding: 6px 10px;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  color: var(--color-ink);
  font-size: 13px;
}
.hygiene-list {
  display: grid;
  gap: 10px;
}
.hygiene-list article {
  position: relative;
  display: grid;
  grid-template-columns: 48px minmax(0, 1fr) 56px;
  align-items: center;
  gap: 14px;
  overflow: hidden;
  padding: 18px 20px 20px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 12px 24px rgba(23, 65, 120, 0.08);
}
.hygiene-list article > b {
  color: var(--color-brand-strong);
  font: 800 13px var(--font-mono);
}
.hygiene-list article div {
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}
.hygiene-list strong {
  color: var(--color-ink);
  font-size: 15px;
  font-weight: 800;
}
.hygiene-list small {
  color: var(--color-text-muted);
  font-size: 12px;
}
.hygiene-list em {
  color: var(--color-brand-strong);
  font: 900 26px var(--font-display);
  font-style: normal;
  text-align: right;
}
.hygiene-list i {
  position: absolute;
  left: 18px;
  right: 18px;
  bottom: 12px;
  height: 4px;
  border-radius: 999px;
  background: rgba(185, 216, 251, 0.6);
}
.hygiene-list i::before {
  display: block;
  width: var(--score);
  height: 100%;
  border-radius: inherit;
  background: var(--color-brand);
  content: '';
}
.hygiene-empty {
  margin: 0;
  padding: 26px;
  color: var(--color-text-muted);
  text-align: center;
  font-size: 14px;
}
.my-room-card {
  margin: 14px 32px 0;
  padding: 22px 26px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: 0 12px 24px rgba(23, 65, 120, 0.08);
}
.my-room-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 16px;
  flex-wrap: wrap;
}
.my-room-head span {
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 15px;
  font-weight: 850;
}
.my-room-head h3 {
  margin: 6px 0 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 24px;
  font-weight: 950;
}
.my-room-actions {
  display: flex;
  align-items: center;
  gap: 10px;
  flex-wrap: wrap;
}
.my-room-toggle {
  min-height: 36px;
  padding: 7px 16px;
  border: 1px solid var(--color-brand-border);
  border-radius: 999px;
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-family: var(--font-body);
  font-size: 13px;
  font-weight: 850;
  cursor: pointer;
  transition:
    transform 0.18s ease,
    box-shadow 0.18s ease;
}
.my-room-toggle:hover {
  transform: translateY(-1px);
  box-shadow: 0 8px 18px rgba(11, 99, 199, 0.08);
}
.my-room-records {
  display: grid;
  gap: 8px;
  margin-top: 14px;
}
.my-room-records article {
  display: grid;
  grid-template-columns: 120px minmax(0, 1fr);
  align-items: center;
  gap: 16px;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.my-room-records time {
  color: var(--color-brand-strong);
  font: 13px var(--font-mono);
  font-weight: 900;
}
.my-room-records strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
}
.my-room-records small {
  display: block;
  margin-top: 3px;
  color: var(--color-text-muted);
  font-size: 13px;
  font-weight: 650;
}
.my-room-empty {
  margin: 0;
  padding: 18px;
  color: var(--color-text-muted);
  text-align: center;
  font-size: 14px;
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
.community-records .revoke-btn {
  border-color: #efc4bc;
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
.community-records .revoke-btn:hover:not(:disabled) {
  border-color: #e0a89e;
}
.community-records .revoke-btn:disabled {
  cursor: not-allowed;
  opacity: 0.5;
}
.community-records .late-btn {
  border-color: #b8d7e8;
  background: #eef8ff;
  color: #276582;
}
.community-records .late-btn:hover:not(:disabled) {
  border-color: #7fb6d6;
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
  color: #fff;
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
  .vote-list,
  .hygiene-board,
  .my-room-card {
    margin: 10px 24px;
  }
  .vote-card {
    grid-template-columns: 1fr;
    gap: 14px;
    padding: 20px 22px;
  }
  .vote-card > time {
    justify-items: center;
    min-height: 46px;
  }
  .vote-actions {
    justify-content: flex-start;
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
  min-width: 0;
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
  min-width: 0;
  gap: 9px;
  color: var(--color-text);
  font-size: 15px;
  font-weight: 850;
}
.community-form input,
.community-form textarea,
.community-form select {
  width: 100%;
  min-width: 0;
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
