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
    if (accommodation?.roomId) requests.push(['votes', studentApi.getRoomVotes(accommodation.roomId)])
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
            <label>
              选择扣分明细
              <select v-model="form.creditRecordId" required>
                <option value="" disabled>选择要申诉的扣分明细</option>
                <option v-for="log in deductibleLogs" :key="log.logId" :value="log.logId">
                  {{ String(log.createTime || '').slice(0, 10) }} · {{ log.scoreChange }} 分 · {{ log.reason }}
                </option>
              </select>
            </label>
            <label>
              申诉原因
              <textarea v-model="form.reason" rows="3" maxlength="200" placeholder="说明申诉事由" required></textarea>
            </label>
          </template>
          <label v-else-if="activeForm === 'late'">
            说明内容
            <textarea v-model="form.reason" rows="3" maxlength="200" placeholder="填写说明" required></textarea>
          </label>
          <template v-else-if="activeForm === 'leave'">
            <label>离校日期 <input type="date" v-model="form.leaveDate" required /></label>
            <label>返校日期 <input type="date" v-model="form.returnDate" required /></label>
            <label>目的地 <input v-model="form.destination" maxlength="200" required placeholder="目的地" /></label>
          </template>
          <template v-else-if="activeForm === 'visitor'">
            <label>访客姓名 <input v-model="form.visitorName" maxlength="50" required placeholder="访客姓名" /></label>
            <label>来访事由 <input v-model="form.visitReason" maxlength="200" placeholder="选填" /></label>
            <label>授权截止 <input type="datetime-local" v-model="form.visitEnd" required /></label>
          </template>
          <template v-else-if="activeForm === 'votes'">
            <label>投票议题 <input v-model="form.topic" maxlength="200" required placeholder="发起什么投票" /></label>
            <label>应参与人数 <input type="number" v-model="form.eligibleCount" min="1" max="99" /></label>
          </template>
          <p v-if="feedback" class="form-feedback" role="status">{{ feedback }}</p>
          <div class="form-actions">
            <button type="button" class="btn" @click="closeForm">取消</button>
            <button type="submit" class="btn btn-primary" :disabled="formLoading">
              {{ formLoading ? '提交中…' : '提交' }}
            </button>
          </div>
        </form>
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
            <StatusTag :label="itemStatus(item)" tone="info" size="small" /><button type="button" @click="toggleDetail(item)">
              查看详情 ↗
            </button>
            <dl v-if="expandedId === itemKey(item)" class="record-detail">
              <template v-for="(value, field) in item" :key="field">
                <div v-if="value !== null && value !== undefined && value !== ''">
                  <dt>{{ field }}</dt>
                  <dd>{{ value }}</dd>
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
  grid-template-columns: 210px minmax(0, 1fr) 235px;
  margin-top: 27px;
  gap: 14px;
}
.community-nav,
.community-workspace,
.community-guide {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.5);
}
.community-nav {
  display: flex;
  flex-direction: column;
}
.community-nav button {
  display: grid;
  grid-template-columns: 28px 1fr auto;
  align-items: center;
  min-height: 70px;
  padding: 11px 14px;
  border: 0;
  border-bottom: 1px solid var(--color-line);
  background: none;
  text-align: left;
  gap: 8px;
  cursor: pointer;
}
.community-nav button:hover,
.community-nav button.active {
  background: var(--color-brand-soft);
}
.community-nav button.active {
  box-shadow: inset 3px 0 var(--color-accent);
}
.community-nav button > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.community-nav button div {
  display: grid;
  gap: 4px;
}
.community-nav button b {
  font-family: var(--font-display);
  font-size: 12px;
}
.community-nav button small {
  color: var(--color-text-muted);
  font-size: 8px;
}
.community-nav button > i {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border: 1px solid var(--color-line-strong);
  color: var(--color-text-muted);
  font: 7px var(--font-mono);
  font-style: normal;
}
.community-workspace > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 112px;
  padding: 19px 21px;
  border-bottom: 1px solid var(--color-line);
  gap: 18px;
}
.community-workspace header span,
.community-guide > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.community-workspace h2,
.community-guide h2 {
  margin: 6px 0;
  font-family: var(--font-display);
  font-size: 21px;
  font-weight: 500;
}
.community-workspace header p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 9px;
}
.community-records article {
  display: grid;
  grid-template-columns: 35px 1fr auto auto;
  align-items: center;
  min-height: 79px;
  padding: 13px 20px;
  border-bottom: 1px solid var(--color-line);
  gap: 12px;
}
.community-records time {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.community-records article div {
  display: grid;
  gap: 5px;
}
.community-records h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 13px;
}
.community-records p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 8px;
}
.community-records article > button {
  border: 0;
  background: none;
  color: var(--color-brand);
  font-size: 8px;
  cursor: pointer;
}
.community-guide {
  padding: 20px;
  background: var(--color-ink);
  color: var(--color-paper);
}
.community-guide h2 {
  margin-bottom: 28px;
}
.community-guide > div {
  padding: 16px 0;
  border-block: 1px solid #40544a;
}
.community-guide > div b {
  font-family: var(--font-display);
  font-size: 13px;
}
.community-guide > div p {
  margin: 8px 0 0;
  color: #8da096;
  font-size: 9px;
  line-height: 1.7;
}
.community-guide footer {
  display: grid;
  margin-top: 25px;
  padding: 14px;
  background: #253b30;
  gap: 5px;
}
.community-guide footer span {
  color: #82968b;
  font-size: 8px;
}
.community-guide footer b {
  font-family: var(--font-display);
  font-size: 12px;
}
@media (max-width: 1000px) {
  .community-layout {
    grid-template-columns: 190px 1fr;
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
    min-width: 145px;
  }
  .community-guide {
    grid-column: auto;
  }
  .community-records article {
    grid-template-columns: 28px 1fr;
  }
  .community-records :deep(.status-tag),
  .community-records article > button {
    grid-column: 2;
  }
}
.community-form {
  display: grid;
  gap: 10px;
  margin: 16px;
  padding: 16px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-surface-muted);
}
.community-form h3 {
  margin: 0;
  font-family: var(--font-display);
  font-size: 16px;
}
.community-form label {
  display: grid;
  gap: 5px;
  color: var(--color-text-muted);
  font-size: 10px;
}
.community-form input,
.community-form textarea,
.community-form select {
  padding: 8px 10px;
  border: 1px solid var(--color-line-strong);
  background: var(--color-paper);
  color: var(--color-ink);
  font-size: 12px;
}
.community-form textarea {
  resize: vertical;
}
.form-feedback {
  margin: 0;
  padding: 9px 11px;
  border-left: 3px solid var(--color-accent);
  background: var(--color-brand-soft);
  color: var(--color-brand);
  font-size: 10px;
}
.form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 8px;
}
.record-detail {
  grid-column: 2;
  margin: 6px 0 0;
  padding: 8px 10px;
  border-left: 2px solid var(--color-accent);
  background: var(--color-surface-muted);
}
.record-detail div {
  display: flex;
  justify-content: space-between;
  gap: 10px;
  padding: 3px 0;
  font-size: 9px;
}
.record-detail dt {
  color: var(--color-text-muted);
}
.record-detail dd {
  margin: 0;
  text-align: right;
}
</style>
