<script setup>
import { computed, onMounted, ref } from 'vue'
import { studentApi } from '@/api/student'
import { InlineState, StatusTag, WorkspaceHeader } from '@/components'
import { useUserStore } from '@/store/user'
import { normalizeCollection } from '@/utils/collection'

const userStore = useUserStore()
const activeMode = ref('booking')
const loading = ref(true)
const error = ref('')
const facilities = ref([])
const sharedItems = ref([])
const loans = ref([])
const selectedResource = ref(null)
const studentId = computed(() => userStore.userInfo?.id || '')

const loadResources = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    studentApi.getFacilities({ page: 1, pageSize: 50 }),
    studentApi.getSharedItems({ page: 1, pageSize: 50 }),
    studentApi.getItemLoans(studentId.value)
  ])
  if (results[0].status === 'fulfilled')
    facilities.value = normalizeCollection(results[0].value).items
  if (results[1].status === 'fulfilled')
    sharedItems.value = normalizeCollection(results[1].value).items
  if (results[2].status === 'fulfilled') loans.value = normalizeCollection(results[2].value).items
  if (results.every((result) => result.status === 'rejected'))
    error.value = '设施与共享物品暂时无法同步'
  loading.value = false
}
const currentResources = computed(() =>
  activeMode.value === 'booking' ? facilities.value : sharedItems.value
)
const resourceName = (item) =>
  item.facilityName ||
  item.itemName ||
  item.name ||
  `资源 #${item.facilityId || item.itemId || item.id}`
const resourceStatus = (item) =>
  item.status || (Number(item.quantity || item.stock || 0) > 0 ? '可用' : '暂无库存')
onMounted(loadResources)
</script>

<template>
  <div class="facilities-page workspace-page">
    <WorkspaceHeader
      eyebrow="STUDENT / SHARED LIFE"
      title="设施预约与共享物品"
      description="预约围绕时间展开，借用围绕库存展开；两种服务使用不同操作路径。"
    >
      <StatusTag :label="`${loans.length} 项借用记录`" tone="info" />
    </WorkspaceHeader>
    <nav class="mode-switch" aria-label="服务类型">
      <button :class="{ active: activeMode === 'booking' }" @click="activeMode = 'booking'">
        设施预约 <span>{{ facilities.length }}</span></button
      ><button :class="{ active: activeMode === 'items' }" @click="activeMode = 'items'">
        共享物品 <span>{{ sharedItems.length }}</span>
      </button>
    </nav>
    <InlineState :loading="loading" :error="error" />

    <div v-if="!loading" class="resource-layout">
      <section class="resource-browser">
        <header>
          <div>
            <span>{{
              activeMode === 'booking' ? 'FACILITIES / SCHEDULE' : 'SHARED / INVENTORY'
            }}</span>
            <h2>{{ activeMode === 'booking' ? '选择可预约设施' : '查看物品库存' }}</h2>
          </div>
          <button class="btn btn-sm" @click="loadResources">刷新资源</button>
        </header>
        <InlineState
          :empty="!currentResources.length"
          :empty-text="activeMode === 'booking' ? '暂无开放设施' : '暂无共享物品'"
        />
        <div class="resource-grid">
          <button
            v-for="(item, index) in currentResources"
            :key="item.facilityId || item.itemId || index"
            :class="{ active: selectedResource === item }"
            @click="selectedResource = item"
          >
            <span>0{{ index + 1 }}</span>
            <h3>{{ resourceName(item) }}</h3>
            <p>{{ item.location || item.description || '服务位置与说明待同步' }}</p>
            <footer>
              <small>{{
                activeMode === 'booking' ? '查看时段' : `库存 ${item.quantity ?? item.stock ?? '—'}`
              }}</small
              ><StatusTag :label="resourceStatus(item)" tone="success" size="small" />
            </footer>
          </button>
        </div>
      </section>

      <aside
        class="schedule-panel"
        :class="{ 'schedule-panel--inventory': activeMode === 'items' }"
      >
        <header>
          <span>{{ activeMode === 'booking' ? 'TIME SLOTS' : 'BORROW FLOW' }}</span>
          <h2>{{ selectedResource ? resourceName(selectedResource) : '选择一项资源' }}</h2>
        </header>
        <template v-if="selectedResource && activeMode === 'booking'"
          ><div class="date-line">
            <button>今天</button><button class="active">明天</button><button>周日</button>
          </div>
          <div class="time-slots">
            <button
              v-for="time in ['08:00', '10:00', '14:00', '16:00', '19:00', '21:00']"
              :key="time"
              :disabled="['10:00', '19:00'].includes(time)"
            >
              {{ time }}<small>{{ ['10:00', '19:00'].includes(time) ? '已占用' : '可预约' }}</small>
            </button>
          </div>
          <button class="btn btn-primary schedule-action">确认预约</button></template
        >
        <template v-else-if="selectedResource"
          ><dl>
            <div>
              <dt>当前库存</dt>
              <dd>{{ selectedResource.quantity ?? selectedResource.stock ?? '—' }}</dd>
            </div>
            <div>
              <dt>信用要求</dt>
              <dd>信用状态正常</dd>
            </div>
            <div>
              <dt>借用期限</dt>
              <dd>以物品规则为准</dd>
            </div>
          </dl>
          <button class="btn btn-primary schedule-action">申请借用</button></template
        >
        <InlineState v-else empty empty-text="从左侧选择设施或物品" />
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
.mode-switch {
  display: flex;
  margin: 27px 0 15px;
  border-bottom: 1px solid var(--color-line-strong);
}
.mode-switch button {
  display: flex;
  align-items: center;
  min-height: 45px;
  padding: 0 20px;
  border: 0;
  border-bottom: 2px solid transparent;
  background: none;
  color: var(--color-text-muted);
  font-size: 11px;
  gap: 9px;
  cursor: pointer;
}
.mode-switch button span {
  display: grid;
  min-width: 20px;
  height: 20px;
  place-items: center;
  border: 1px solid var(--color-line-strong);
  font: 8px var(--font-mono);
}
.mode-switch button.active {
  border-color: var(--color-accent);
  color: var(--color-ink);
  font-weight: 600;
}
.resource-layout {
  display: grid;
  grid-template-columns: minmax(0, 1.35fr) minmax(300px, 0.62fr);
  gap: 16px;
}
.resource-browser,
.schedule-panel {
  border: 1px solid var(--color-line-strong);
  background: rgba(250, 246, 237, 0.52);
}
.resource-browser > header,
.schedule-panel > header {
  display: flex;
  align-items: end;
  justify-content: space-between;
  min-height: 72px;
  padding: 16px 19px;
  border-bottom: 1px solid var(--color-line);
}
.resource-browser header span,
.schedule-panel header span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.15em;
}
.resource-browser h2,
.schedule-panel h2 {
  margin: 6px 0 0;
  font-family: var(--font-display);
  font-size: 19px;
  font-weight: 500;
}
.resource-grid {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
}
.resource-grid > button {
  display: grid;
  min-height: 160px;
  padding: 18px;
  border: 0;
  border-right: 1px solid var(--color-line);
  border-bottom: 1px solid var(--color-line);
  background: transparent;
  text-align: left;
  cursor: pointer;
}
.resource-grid > button:hover,
.resource-grid > button.active {
  background: var(--color-brand-soft);
}
.resource-grid > button.active {
  box-shadow: inset 3px 0 var(--color-accent);
}
.resource-grid > button > span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
}
.resource-grid h3 {
  align-self: end;
  margin: 20px 0 4px;
  font-family: var(--font-display);
  font-size: 16px;
}
.resource-grid p {
  margin: 0;
  color: var(--color-text-muted);
  font-size: 9px;
}
.resource-grid footer {
  display: flex;
  align-items: end;
  justify-content: space-between;
  margin-top: 14px;
}
.resource-grid footer small {
  color: var(--color-text-soft);
  font-size: 8px;
}
.schedule-panel {
  background: var(--color-ink);
  color: var(--color-paper);
}
.schedule-panel > header {
  border-color: #405249;
}
.date-line {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  padding: 18px 18px 0;
  gap: 7px;
}
.date-line button {
  padding: 8px;
  border: 1px solid #4e6257;
  background: transparent;
  color: #8fa096;
  font-size: 9px;
}
.date-line button.active {
  border-color: #d48769;
  color: #f4edde;
}
.time-slots {
  display: grid;
  grid-template-columns: 1fr 1fr;
  padding: 13px 18px;
  gap: 7px;
}
.time-slots button {
  display: grid;
  padding: 12px;
  border: 1px solid #4d6156;
  background: #203329;
  color: #f3ebdd;
  font: 10px var(--font-mono);
  text-align: left;
  gap: 5px;
  cursor: pointer;
}
.time-slots button small {
  color: #799084;
  font: 8px var(--font-body);
}
.time-slots button:disabled {
  opacity: 0.4;
}
.schedule-action {
  width: calc(100% - 36px);
  margin: 8px 18px;
}
.schedule-panel dl {
  margin: 20px 18px;
}
.schedule-panel dl div {
  display: flex;
  justify-content: space-between;
  padding: 15px 0;
  border-bottom: 1px solid #41564a;
}
.schedule-panel dt {
  color: #83968a;
  font-size: 9px;
}
.schedule-panel dd {
  margin: 0;
  font-family: var(--font-display);
  font-size: 12px;
}
@media (max-width: 850px) {
  .workspace-page {
    width: min(100% - 32px, var(--content-max));
  }
  .resource-layout {
    grid-template-columns: 1fr;
  }
}
@media (max-width: 560px) {
  .resource-grid {
    grid-template-columns: 1fr;
  }
}
</style>
