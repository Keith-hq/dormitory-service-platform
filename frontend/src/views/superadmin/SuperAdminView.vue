<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { useRoute } from 'vue-router'
import { governanceApi } from '@/api/governance'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const route = useRoute(),
  loading = ref(true),
  error = ref(''),
  admins = ref([]),
  students = ref([]),
  audits = ref([]),
  report = ref(null)
const section = computed(() => route.meta.section ?? 'overview')
const title = computed(
  () => ({ overview: '治理总览', people: '人员档案', audit: '审计与报表' })[section.value]
)
const metrics = computed(() => [
  { label: '学生档案', value: students.value.length, hint: '全校范围' },
  { label: '管理人员', value: admins.value.length, hint: '在册账号' },
  { label: '审计事件', value: audits.value.length, hint: '最近记录' },
  { label: '角色类型', value: new Set(admins.value.map((x) => x.roleLevel)).size, hint: '权限治理' }
])
const load = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    governanceApi.getAdmins(),
    governanceApi.getStudents(),
    governanceApi.getAuditEvents({ page: 1, pageSize: 30 }),
    governanceApi.getReport('occupancy', { yearMonth: new Date().toISOString().slice(0, 7) })
  ])
  const targets = [admins, students, audits]
  results.slice(0, 3).forEach((result, index) => {
    if (result.status === 'fulfilled')
      targets[index].value = normalizeCollection(result.value).items
  })
  if (results[3].status === 'fulfilled') report.value = results[3].value
  if (results.every((x) => x.status === 'rejected'))
    error.value = toUserMessage(results[0].reason, '治理数据暂时无法同步')
  loading.value = false
}
const display = (row, ...keys) =>
  keys
    .map((key) => row[key])
    .find((value) => value !== undefined && value !== null && value !== '') ?? '—'
watch(section, load)
onMounted(load)
</script>

<template>
  <main class="governance-page">
    <WorkspaceHeader
      eyebrow="PLATFORM GOVERNANCE"
      :title="title"
      description="跨楼栋查看人员、权限和关键操作留痕，所有高风险动作均保留审计依据。"
      ><button class="btn btn-sm" @click="load">刷新</button></WorkspaceHeader
    >
    <MetricStrip :metrics="metrics" style="--metric-count: 4" /><InlineState
      :loading="loading"
      :error="error"
    />
    <section v-if="!loading && !error && section === 'overview'" class="overview-grid">
      <article>
        <span>01 / OCCUPANCY</span>
        <h2>入住概览</h2>
        <pre>{{ report?.reportData ? '本月入住报表已生成' : '本月暂无入住报表' }}</pre>
      </article>
      <article>
        <span>02 / ROLE DISTRIBUTION</span>
        <h2>管理角色</h2>
        <div v-for="role in ['超级管理员', '楼长', '维修员']" :key="role">
          <strong>{{ role }}</strong
          ><b>{{ admins.filter((x) => x.roleLevel === role).length }}</b>
        </div>
      </article>
    </section>
    <section v-else-if="!loading && !error && section === 'people'" class="people-board">
      <article>
        <header>
          <span>ADMIN DIRECTORY</span><b>{{ admins.length }}</b>
        </header>
        <div v-for="item in admins" :key="item.adminId">
          <strong>{{ display(item, 'adminName', 'name') }}</strong
          ><small>{{ display(item, 'adminId') }} · {{ display(item, 'roleLevel') }}</small>
          <p>{{ display(item, 'buildingName', 'phone') }}</p>
        </div>
      </article>
      <article>
        <header>
          <span>STUDENT DIRECTORY</span><b>{{ students.length }}</b>
        </header>
        <div v-for="item in students.slice(0, 30)" :key="item.studentId">
          <strong>{{ display(item, 'name') }}</strong
          ><small>{{ display(item, 'studentId') }} · {{ display(item, 'majorName') }}</small>
          <p>{{ display(item, 'collegeName', 'phone') }}</p>
        </div>
      </article>
    </section>
    <section v-else-if="!loading && !error" class="audit-stream">
      <header><span>TIME</span><span>ACTOR</span><span>EVENT</span><span>TARGET</span></header>
      <article v-for="item in audits" :key="item.auditId">
        <time>{{ display(item, 'eventTime') }}</time
        ><strong>{{ display(item, 'actorLoginName', 'actorAccountId') }}</strong>
        <p>{{ display(item, 'eventType') }}</p>
        <small>{{ display(item, 'targetType') }} / {{ display(item, 'targetId') }}</small>
      </article>
      <p v-if="!audits.length" class="empty">暂无审计记录</p>
    </section>
  </main>
</template>

<style scoped>
.governance-page {
  width: min(100% - 40px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.overview-grid {
  display: grid;
  grid-template-columns: 1.2fr 0.8fr;
  gap: 28px;
  margin-top: 30px;
}
.overview-grid article {
  min-height: 300px;
  padding: 30px;
  border: 1px solid var(--color-line-strong);
  background: rgba(255, 255, 255, 0.35);
}
.overview-grid span,
.people-board header span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.13em;
}
.overview-grid h2 {
  font: 500 28px var(--font-display);
}
.overview-grid pre {
  display: grid;
  min-height: 150px;
  place-items: center;
  background: var(--color-ink);
  color: #d9d1c4;
  font: 11px var(--font-mono);
}
.overview-grid article div {
  display: flex;
  justify-content: space-between;
  padding: 20px 0;
  border-bottom: 1px solid var(--color-line);
}
.overview-grid article div strong {
  font-size: 12px;
}
.overview-grid article div b {
  font: 500 24px var(--font-display);
}
.people-board {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 28px;
  margin-top: 30px;
}
.people-board > article {
  border: 1px solid var(--color-line-strong);
}
.people-board header {
  display: flex;
  justify-content: space-between;
  padding: 18px 20px;
  border-bottom: 1px solid var(--color-line-strong);
}
.people-board article > div {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 5px;
  padding: 15px 20px;
  border-bottom: 1px solid var(--color-line);
}
.people-board strong {
  font-size: 11px;
}
.people-board small {
  color: var(--color-text-muted);
  font: 9px var(--font-mono);
}
.people-board p {
  grid-column: 1/-1;
  margin: 0;
  color: var(--color-text-soft);
  font-size: 9px;
}
.audit-stream {
  margin-top: 30px;
  border-top: 1px solid var(--color-line-strong);
}
.audit-stream header,
.audit-stream article {
  display: grid;
  grid-template-columns: 180px 140px 1fr 180px;
  gap: 18px;
  padding: 14px 16px;
  border-bottom: 1px solid var(--color-line);
}
.audit-stream header {
  color: var(--color-text-soft);
  font: 8px var(--font-mono);
}
.audit-stream time,
.audit-stream small {
  font: 9px var(--font-mono);
  color: var(--color-text-muted);
}
.audit-stream strong,
.audit-stream p {
  margin: 0;
  font-size: 10px;
}
.empty {
  padding: 28px;
  color: var(--color-text-muted);
}
@media (max-width: 800px) {
  .overview-grid,
  .people-board {
    grid-template-columns: 1fr;
  }
  .audit-stream header {
    display: none;
  }
  .audit-stream article {
    grid-template-columns: 1fr;
  }
}
</style>
