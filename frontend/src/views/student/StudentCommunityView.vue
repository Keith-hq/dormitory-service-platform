<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

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
          <button class="btn btn-primary">
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
            <StatusTag :label="itemStatus(item)" tone="info" size="small" /><button type="button">
              查看详情 ↗
            </button>
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
</style>
