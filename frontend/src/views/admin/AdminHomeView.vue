<script setup>
import { computed, onMounted, ref } from 'vue'
import { adminApi } from '@/api/admin'
import { buildingApi } from '@/api/building'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const loading = ref(true)
const error = ref('')
const buildings = ref([])
const density = ref([])
const accessLogs = ref([])
const violations = ref([])
const notices = ref([])

const abnormalLogs = computed(() =>
  accessLogs.value.filter((item) => {
    const status = String(item.status ?? item.accessStatus ?? '').toLowerCase()
    return status && !['normal', '正常'].includes(status)
  })
)
const metrics = computed(() => [
  { label: '在管楼栋', value: buildings.value.length, hint: '空间底座' },
  {
    label: '当前在楼',
    value: density.value.reduce(
      (sum, item) => sum + Number(item.currentCount ?? item.count ?? 0),
      0
    ),
    hint: '实时门禁'
  },
  { label: '异常通行', value: abnormalLogs.value.length, hint: '优先核查' },
  { label: '违规记录', value: violations.value.length, hint: '当前页' }
])

const load = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    buildingApi.getList({ page: 1, pageSize: 100 }),
    adminApi.getAccessDensity(),
    adminApi.getAccessLogs({ page: 1, pageSize: 8 }),
    adminApi.getViolations({ page: 1, pageSize: 8 }),
    adminApi.getNotices({ page: 1, pageSize: 4 })
  ])
  const targets = [buildings, density, accessLogs, violations, notices]
  results.forEach((result, index) => {
    if (result.status === 'fulfilled')
      targets[index].value = normalizeCollection(result.value).items
  })
  const failures = results.filter((result) => result.status === 'rejected')
  if (failures.length === results.length)
    error.value = toUserMessage(failures[0].reason, '运营数据暂时无法同步')
  loading.value = false
}

const displayTime = (item) => item.accessTime ?? item.occurredAt ?? item.createTime ?? '待记录'
const displayStudent = (item) => item.studentName ?? item.studentId ?? '未识别人员'

onMounted(load)
</script>

<template>
  <main class="admin-home page-frame">
    <WorkspaceHeader
      eyebrow="DORMITORY OPERATIONS"
      title="今日宿舍运营"
      description="用一张值班简报看清楼栋运行、安全异常和需要交接的事项。"
    >
      <button class="btn btn-sm" type="button" :disabled="loading" @click="load">重新同步</button>
    </WorkspaceHeader>

    <MetricStrip :metrics="metrics" style="--metric-count: 4" />
    <InlineState :loading="loading" :error="error" />

    <section v-if="!loading" class="brief-grid">
      <article class="duty-brief">
        <header>
          <span>01 / DUTY PRIORITY</span>
          <h2>值班优先级</h2>
        </header>
        <ol>
          <li v-for="item in abnormalLogs.slice(0, 5)" :key="item.logId ?? item.accessLogId">
            <time>{{ displayTime(item) }}</time>
            <div>
              <strong>{{ displayStudent(item) }}</strong>
              <p>
                {{ item.roomName ?? item.buildingName ?? '位置待核对' }} ·
                {{ item.direction ?? item.status ?? '异常通行' }}
              </p>
            </div>
          </li>
          <li v-if="!abnormalLogs.length" class="quiet">
            <strong>当前没有待核查的门禁异常</strong>
            <p>值班台会在发现异常后自动排列到这里。</p>
          </li>
        </ol>
        <router-link to="/admin/duty">进入安全值班台 →</router-link>
      </article>

      <aside class="building-pulse">
        <header>
          <span>02 / BUILDING PULSE</span>
          <h2>楼栋脉搏</h2>
        </header>
        <div
          v-for="(item, index) in (density.length ? density : buildings).slice(0, 6)"
          :key="item.buildingId ?? index"
          class="pulse-row"
        >
          <div>
            <strong>{{ item.buildingName ?? `楼栋 ${index + 1}` }}</strong
            ><small>{{ item.buildingType ?? '实时在楼人数' }}</small>
          </div>
          <b>{{ item.currentCount ?? item.count ?? item.floorCount ?? '—' }}</b>
        </div>
        <router-link to="/building">维护空间档案 →</router-link>
      </aside>
    </section>

    <section v-if="!loading" class="handover-board">
      <header>
        <div>
          <span>03 / HANDOVER</span>
          <h2>公告与交接</h2>
        </div>
        <small>{{ notices.length }} 条近期公告</small>
      </header>
      <div class="notice-list">
        <article v-for="(notice, index) in notices" :key="notice.noticeId ?? index">
          <span>0{{ index + 1 }}</span>
          <div>
            <strong>{{ notice.title ?? notice.noticeTitle ?? '宿舍通知' }}</strong>
            <p>{{ notice.content ?? notice.summary ?? '查看通知详情并纳入本班交接。' }}</p>
          </div>
        </article>
        <p v-if="!notices.length" class="empty-copy">暂无近期公告，重要事项可在公告模块发布。</p>
      </div>
    </section>
  </main>
</template>

<style scoped>
.page-frame {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.brief-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.45fr) minmax(280px, 0.75fr);
  gap: 28px;
  margin-top: 34px;
}
.brief-grid header span,
.handover-board header span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.14em;
}
.brief-grid h2,
.handover-board h2 {
  margin: 7px 0 0;
  font: 500 24px var(--font-display);
  color: var(--color-ink);
}
.duty-brief {
  padding: 28px;
  background: var(--color-ink);
  color: #fff;
}
.duty-brief h2 {
  color: #fff;
}
.duty-brief ol {
  list-style: none;
  padding: 0;
  margin: 24px 0;
}
.duty-brief li {
  display: grid;
  grid-template-columns: 110px 1fr;
  gap: 18px;
  padding: 15px 0;
  border-top: 1px solid rgba(255, 255, 255, 0.14);
}
.duty-brief time {
  font: 10px var(--font-mono);
  color: #cbbca4;
}
.duty-brief strong {
  font-size: 13px;
}
.duty-brief p {
  margin: 4px 0 0;
  color: #aaa39a;
  font-size: 11px;
}
.duty-brief a,
.building-pulse a {
  color: var(--color-accent);
  font-size: 11px;
  font-weight: 700;
}
.building-pulse {
  padding: 28px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.35);
}
.pulse-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 15px 0;
  border-bottom: 1px solid var(--color-line);
}
.pulse-row div {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.pulse-row strong {
  font-size: 12px;
}
.pulse-row small {
  color: var(--color-text-soft);
  font-size: 9px;
}
.pulse-row b {
  font: 500 25px var(--font-display);
}
.building-pulse a {
  display: block;
  margin-top: 20px;
}
.handover-board {
  margin-top: 34px;
  padding-top: 26px;
  border-top: 1px solid var(--color-line-strong);
}
.handover-board > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
}
.notice-list {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  margin-top: 20px;
  border: 1px solid var(--color-line-strong);
}
.notice-list article {
  display: grid;
  grid-template-columns: 32px 1fr;
  gap: 14px;
  padding: 22px;
  border-right: 1px solid var(--color-line);
  border-bottom: 1px solid var(--color-line);
}
.notice-list article > span {
  font: 10px var(--font-mono);
  color: var(--color-accent-strong);
}
.notice-list strong {
  font-size: 12px;
}
.notice-list p {
  margin: 6px 0 0;
  color: var(--color-text-muted);
  font-size: 10px;
  line-height: 1.7;
}
.empty-copy {
  padding: 24px;
  color: var(--color-text-muted);
}
@media (max-width: 860px) {
  .brief-grid,
  .notice-list {
    grid-template-columns: 1fr;
  }
  .duty-brief li {
    grid-template-columns: 1fr;
    gap: 5px;
  }
}
</style>
