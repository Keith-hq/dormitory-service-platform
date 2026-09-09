<script setup>
import { computed, onMounted, ref } from 'vue'
import { assetApi } from '@/api/assets'
import { InlineState, MetricStrip, WorkspaceHeader } from '@/components'
import { normalizeCollection } from '@/utils/collection'
import { toUserMessage } from '@/utils/errorMessage'

const activeDesk = ref('assets')
const loading = ref(true)
const working = ref('')
const error = ref('')
const feedback = ref('')
const roomId = ref('')
const assets = ref([])
const warnings = ref([])
const cleaningRequests = ref([])
const sharedItems = ref([])
const warningPage = ref(1)
const warningTotal = ref(0)
const PAGE_SIZE = 20
const selectedAsset = ref(null)
const selectedItem = ref(null)
const assetForm = ref({ roomId: '', assetName: '', quantity: 1, status: '正常' })
const stocktake = ref({ quantity: 1, note: '' })
const repairDescription = ref('')
const sharedForm = ref({ name: '', quantity: 1, buildingId: '', description: '' })
const sharedEdit = ref({ quantity: 1, status: '正常', description: '' })

const pendingCleaning = computed(
  () => cleaningRequests.value.filter((item) => item.status !== '已完成').length
)
const activeWarnings = computed(() => warnings.value.filter((item) => item.handled !== '是').length)
const warningTotalPages = computed(() => Math.max(1, Math.ceil(warningTotal.value / PAGE_SIZE)))
const metrics = computed(() => [
  {
    label: '当前房间资产',
    value: assets.value.length,
    hint: roomId.value ? `房间 ${roomId.value}` : '待查询'
  },
  { label: '待处理预警', value: activeWarnings.value, hint: '损耗与缺失' },
  { label: '待办保洁', value: pendingCleaning.value, hint: '宿舍/楼栋' },
  { label: '共享物品', value: sharedItems.value.length, hint: '当前可借条目' }
])

const run = async (key, action, success, refresh) => {
  working.value = key
  feedback.value = ''
  try {
    await action()
    feedback.value = success
    if (refresh) await refresh()
    return true
  } catch (e) {
    feedback.value = toUserMessage(e, '操作未完成，请核对输入后重试')
    return false
  } finally {
    working.value = ''
  }
}

const loadRoomAssets = async () => {
  if (!roomId.value) return
  error.value = ''
  try {
    assets.value = normalizeCollection(await assetApi.getRoomAssets(roomId.value)).items
    if (selectedAsset.value) {
      const current = assets.value.find((item) => item.assetId === selectedAsset.value.assetId)
      selectedAsset.value = current ?? null
      if (current) stocktake.value.quantity = current.quantity ?? 0
    }
  } catch (e) {
    error.value = toUserMessage(e, '房间资产暂时无法同步')
  }
}

const loadOperations = async () => {
  loading.value = true
  error.value = ''
  const results = await Promise.allSettled([
    assetApi.getWarnings({ page: warningPage.value, pageSize: PAGE_SIZE }),
    assetApi.getCleaningRequests({ page: 1, pageSize: 50 }),
    assetApi.getSharedItems()
  ])
  if (results[0].status === 'fulfilled') {
    const normalized = normalizeCollection(results[0].value)
    warnings.value = normalized.items
    warningTotal.value = normalized.total
  }
  if (results[1].status === 'fulfilled') {
    cleaningRequests.value = normalizeCollection(results[1].value).items
  }
  if (results[2].status === 'fulfilled') {
    sharedItems.value = normalizeCollection(results[2].value).items
    if (selectedItem.value) {
      const current = sharedItems.value.find((item) => item.itemId === selectedItem.value.itemId)
      selectedItem.value = current ?? null
      if (current) {
        sharedEdit.value = {
          quantity: current.totalQty ?? 0,
          status: current.status || '正常',
          description: current.description || ''
        }
      }
    }
  }
  const failedCount = results.filter((result) => result.status === 'rejected').length
  if (failedCount === results.length) {
    error.value = '运营数据暂时无法同步，请确认后端服务与数据库迁移状态。'
  } else if (failedCount > 0) {
    error.value = `已有 ${results.length - failedCount}/${results.length} 组运营数据同步成功，其余接口暂时不可用。`
  }
  loading.value = false
}

const completeCleaningRequest = async (ticketId) => {
  working.value = `creq-${ticketId}`
  try {
    await assetApi.completeCleaningRequest(ticketId)
    await loadOperations()
  } catch {
    /* 保留旧状态，交由整体同步提示 */
  } finally {
    working.value = ''
  }
}

const goOperationPage = async (kind, nextPage) => {
  if (kind !== 'warnings') return
  if (nextPage < 1 || nextPage > warningTotalPages.value || nextPage === warningPage.value) return
  warningPage.value = nextPage
  await loadOperations()
}

const createAsset = async () => {
  const ok = await run(
    'asset-create',
    () =>
      assetApi.createAsset({
        roomId: Number(assetForm.value.roomId),
        assetName: assetForm.value.assetName,
        quantity: Number(assetForm.value.quantity),
        status: assetForm.value.status
      }),
    '资产已登记'
  )
  if (ok) {
    roomId.value = assetForm.value.roomId
    assetForm.value = { roomId: '', assetName: '', quantity: 1, status: '正常' }
    await loadRoomAssets()
  }
}

const chooseAsset = (item) => {
  selectedAsset.value = item
  stocktake.value = { quantity: item.quantity ?? 0, note: '' }
  repairDescription.value = ''
}

const updateAssetStatus = (status) =>
  run(
    `asset-${status}`,
    () => assetApi.updateAsset(selectedAsset.value.assetId, { status }),
    `资产状态已更新为“${status}”`,
    loadRoomAssets
  )

const submitStocktake = () =>
  run(
    'stocktake',
    () =>
      assetApi.stocktakeAsset(selectedAsset.value.assetId, {
        quantity: Number(stocktake.value.quantity),
        note: stocktake.value.note || null
      }),
    '资产盘点已保存',
    loadRoomAssets
  )

const sendToRepair = () =>
  run(
    'asset-repair',
    () =>
      assetApi.sendAssetToRepair(selectedAsset.value.assetId, {
        description: repairDescription.value
      }),
    '已生成维修记录',
    loadOperations
  )

const deleteAsset = async () => {
  if (
    !window.confirm(
      `确认删除资产“${selectedAsset.value.assetName}”吗？已有报修关联时后端会拒绝删除。`
    )
  )
    return
  const ok = await run(
    'asset-delete',
    () => assetApi.deleteAsset(selectedAsset.value.assetId),
    '资产已删除',
    loadRoomAssets
  )
  if (ok) selectedAsset.value = null
}

const handleWarning = (item, action) =>
  run(
    `warning-${item.assetId}`,
    () => assetApi.handleWarning(item.assetId, { action, note: null }),
    `预警已${action}`,
    loadOperations
  )

const createSharedItem = async () => {
  const ok = await run(
    'shared-create',
    () =>
      assetApi.createSharedItem({
        name: sharedForm.value.name,
        quantity: Number(sharedForm.value.quantity),
        buildingId: Number(sharedForm.value.buildingId),
        description: sharedForm.value.description || null
      }),
    '共享物品已发布',
    loadOperations
  )
  if (ok) sharedForm.value = { name: '', quantity: 1, buildingId: '', description: '' }
}

const chooseItem = (item) => {
  selectedItem.value = item
  sharedEdit.value = {
    quantity: item.totalQty ?? 0,
    status: item.status || '正常',
    description: item.description || ''
  }
}

const updateSharedItem = () =>
  run(
    'shared-update',
    () =>
      assetApi.updateSharedItem(selectedItem.value.itemId, {
        quantity: Number(sharedEdit.value.quantity),
        status: sharedEdit.value.status,
        description: sharedEdit.value.description || null
      }),
    '共享物品已更新',
    loadOperations
  )

const deleteSharedItem = async () => {
  if (
    !window.confirm(
      `确认删除共享物品“${selectedItem.value.itemName}”吗？已有借出记录时后端会拒绝删除。`
    )
  )
    return
  const ok = await run(
    'shared-delete',
    () => assetApi.deleteSharedItem(selectedItem.value.itemId),
    '共享物品已删除',
    loadOperations
  )
  if (ok) selectedItem.value = null
}

onMounted(loadOperations)
</script>

<template>
  <main class="assets-page">
    <WorkspaceHeader
      eyebrow="ASSET & SERVICE CONTROL"
      title="资产与保洁"
      description="按房间维护固定资产，以预警和保洁任务承接日常运营；共享物品库存从同一工作台发布。"
    />
    <MetricStrip :metrics="metrics" style="--metric-count: 4" />
    <InlineState :loading="loading" :error="error" />
    <p v-if="feedback" class="feedback">{{ feedback }}</p>

    <nav class="desk-tabs" aria-label="资产运营工作台">
      <button
        v-for="desk in [
          ['assets', '房间资产'],
          ['cleaning', '保洁任务'],
          ['shared', '共享物品']
        ]"
        :key="desk[0]"
        :class="{ active: activeDesk === desk[0] }"
        @click="activeDesk = desk[0]"
      >
        {{ desk[1] }}
      </button>
    </nav>

    <section v-if="activeDesk === 'assets'" class="work-grid">
      <article class="ledger panel">
        <header>
          <span>01 / ROOM LEDGER</span>
          <h2>房间资产账</h2>
        </header>
        <form class="query" @submit.prevent="loadRoomAssets">
          <label>房间 ID<input v-model="roomId" required min="1" type="number" /></label>
          <button class="btn btn-primary">查询资产</button>
        </form>
        <div class="record-list">
          <button
            v-for="item in assets"
            :key="item.assetId"
            :class="{ selected: selectedAsset?.assetId === item.assetId }"
            @click="chooseAsset(item)"
          >
            <b>#{{ item.assetId }}</b
            ><span>{{ item.assetName }}</span
            ><em>{{ item.quantity ?? 0 }} 件</em><small>{{ item.status }}</small>
          </button>
          <p v-if="roomId && !assets.length">该房间暂无资产记录。</p>
          <p v-else-if="!roomId">输入房间 ID 查询并开始盘点。</p>
        </div>
      </article>

      <aside class="actions-stack">
        <article class="panel">
          <header>
            <span>02 / REGISTER</span>
            <h2>登记资产</h2>
          </header>
          <form class="form-grid" @submit.prevent="createAsset">
            <label
              >房间 ID<input v-model="assetForm.roomId" required min="1" type="number"
            /></label>
            <label
              >资产名称<input v-model.trim="assetForm.assetName" required maxlength="50"
            /></label>
            <label
              >数量<input v-model="assetForm.quantity" required min="0" max="999" type="number"
            /></label>
            <label
              >状态<select v-model="assetForm.status">
                <option>正常</option>
                <option>损坏</option>
                <option>缺失</option>
              </select></label
            >
            <button class="btn btn-primary" :disabled="working === 'asset-create'">登记</button>
          </form>
        </article>
        <article v-if="selectedAsset" class="panel selected-card">
          <header>
            <span>03 / STOCKTAKE</span>
            <h2>{{ selectedAsset.assetName }}</h2>
          </header>
          <div class="status-actions">
            <button
              v-for="status in ['正常', '损坏', '缺失']"
              :key="status"
              class="btn btn-sm"
              @click="updateAssetStatus(status)"
            >
              {{ status }}
            </button>
          </div>
          <form class="form-grid" @submit.prevent="submitStocktake">
            <label
              >盘点数量<input v-model="stocktake.quantity" required min="0" max="999" type="number"
            /></label>
            <label>盘点说明<input v-model.trim="stocktake.note" maxlength="500" /></label>
            <button class="btn btn-primary" :disabled="working === 'stocktake'">保存盘点</button>
          </form>
          <form class="repair-row" @submit.prevent="sendToRepair">
            <input
              v-model.trim="repairDescription"
              required
              maxlength="500"
              placeholder="损坏说明，提交后进入维修流程"
            />
            <button class="btn" :disabled="working === 'asset-repair'">转报修</button>
          </form>
          <button class="delete-action" :disabled="working === 'asset-delete'" @click="deleteAsset">
            删除当前资产
          </button>
        </article>
      </aside>

      <article class="warnings panel full-span">
        <header>
          <span>04 / LOSS WARNINGS</span>
          <h2>损耗预警</h2>
        </header>
        <div class="table-list">
          <div v-for="item in warnings" :key="item.warningId">
            <b>{{ item.assetName }}</b
            ><span>房间 {{ item.roomId ?? '—' }} · 资产 #{{ item.assetId }}</span
            ><em>{{ item.handled === '是' ? item.handleAction : '待处理' }}</em>
            <template v-if="item.handled !== '是'">
              <button class="btn btn-sm" @click="handleWarning(item, '标记重点')">标记重点</button>
              <button class="btn btn-sm btn-primary" @click="handleWarning(item, '处理')">
                完成处理
              </button>
            </template>
          </div>
          <p v-if="!warnings.length">当前没有损耗预警。</p>
        </div>
        <nav class="pager" aria-label="损耗预警分页">
          <button
            class="btn btn-sm"
            :disabled="warningPage <= 1"
            @click="goOperationPage('warnings', warningPage - 1)"
          >
            上一页
          </button>
          <span>第 {{ warningPage }} / {{ warningTotalPages }} 页 · 共 {{ warningTotal }} 条</span>
          <button
            class="btn btn-sm"
            :disabled="warningPage >= warningTotalPages"
            @click="goOperationPage('warnings', warningPage + 1)"
          >
            下一页
          </button>
        </nav>
      </article>
    </section>

    <section v-else-if="activeDesk === 'cleaning'" class="panel single-desk">
      <header>
        <span>01 / CLEANING REQUESTS</span>
        <h2>保洁队列</h2>
      </header>
      <div class="cleaning-board cleaning-rail">
        <template v-if="cleaningRequests.length">
          <article v-for="req in cleaningRequests" :key="req.taskId">
            <span>#{{ req.taskId }}</span>
            <h3>
              {{
                req.sourceType === '宿舍申请'
                  ? `房间 ${req.roomNumber || req.roomId} 保洁申请`
                  : req.sourceType === '楼栋整体'
                    ? `${req.buildingName || '楼栋'} 整体保洁`
                    : '保洁请求'
              }}
            </h3>
            <p>
              {{ req.description }} · 申请人
              {{ req.requesterName || req.requesterStudentId || '系统' }} · {{ req.status }}
            </p>
            <button
              v-if="req.status !== '已完成'"
              class="btn btn-primary"
              :disabled="working === `creq-${req.taskId}`"
              @click="completeCleaningRequest(req.taskId)"
            >
              标记完成
            </button>
            <small v-else>已完成</small>
          </article>
        </template>
        <p v-else>暂无保洁请求（学生申请后会显示在这里，楼栋整体保洁每周一生成）。</p>
      </div>
    </section>

    <section v-else class="work-grid shared-desk">
      <article class="panel">
        <header>
          <span>01 / SHARED INVENTORY</span>
          <h2>共享物品库存</h2>
        </header>
        <div class="record-list">
          <button
            v-for="item in sharedItems"
            :key="item.itemId"
            :class="{ selected: selectedItem?.itemId === item.itemId }"
            @click="chooseItem(item)"
          >
            <b>#{{ item.itemId }}</b
            ><span>{{ item.itemName }}</span
            ><em>{{ item.availableQty }}/{{ item.totalQty }}</em
            ><small>{{ item.status }}</small>
          </button>
          <p v-if="!sharedItems.length">暂无共享物品。</p>
        </div>
      </article>
      <aside class="actions-stack">
        <article class="panel">
          <header>
            <span>02 / PUBLISH</span>
            <h2>发布物品</h2>
          </header>
          <form class="form-grid" @submit.prevent="createSharedItem">
            <label>物品名称<input v-model.trim="sharedForm.name" required maxlength="50" /></label>
            <label
              >楼栋 ID<input v-model="sharedForm.buildingId" required min="1" type="number"
            /></label>
            <label
              >总数量<input v-model="sharedForm.quantity" required min="0" max="999" type="number"
            /></label>
            <label>说明<input v-model.trim="sharedForm.description" maxlength="200" /></label>
            <button class="btn btn-primary" :disabled="working === 'shared-create'">发布</button>
          </form>
        </article>
        <article v-if="selectedItem" class="panel">
          <header>
            <span>03 / MAINTAIN</span>
            <h2>{{ selectedItem.itemName }}</h2>
          </header>
          <form class="form-grid" @submit.prevent="updateSharedItem">
            <label
              >总数量<input v-model="sharedEdit.quantity" required min="0" max="999" type="number"
            /></label>
            <label
              >状态<select v-model="sharedEdit.status">
                <option>正常</option>
                <option>停用</option>
              </select></label
            >
            <label class="wide"
              >说明<input v-model.trim="sharedEdit.description" maxlength="200"
            /></label>
            <button class="btn btn-primary" :disabled="working === 'shared-update'">
              保存维护
            </button>
          </form>
          <button
            class="delete-action"
            :disabled="working === 'shared-delete'"
            @click="deleteSharedItem"
          >
            删除当前物品
          </button>
        </article>
      </aside>
    </section>
  </main>
</template>

<style scoped>
.assets-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: 72px;
}
.feedback {
  margin: 18px 0 0;
  padding: 12px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-size: 13px;
}
.pager {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-top: 16px;
  padding: 14px 18px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
  color: var(--color-text-muted);
  font-size: 12px;
}
.desk-tabs {
  display: flex;
  gap: 8px;
  margin-top: 28px;
  padding: 8px;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.desk-tabs button {
  min-width: 150px;
  min-height: 44px;
  padding: 0 18px;
  color: var(--color-text-muted);
  background: transparent;
  border: 0;
  border-radius: var(--radius-lg);
  font-weight: 800;
  cursor: pointer;
}
.desk-tabs button.active {
  background: var(--color-brand);
  color: #fff;
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.18);
}
.work-grid {
  display: grid;
  grid-template-columns: minmax(0, 1.25fr) minmax(360px, 0.75fr);
  gap: 24px;
  padding: 28px 0 0;
}
.panel {
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}
.panel > header {
  display: flex;
  align-items: flex-end;
  justify-content: space-between;
  gap: 20px;
  padding: 22px 24px 12px;
}
.panel header span {
  color: var(--color-brand-strong);
  font: 800 12px/1.4 var(--font-mono);
  letter-spacing: 0.12em;
}
.panel header h2 {
  margin: 6px 0 0;
  color: var(--color-ink);
  font: 900 24px/1.2 var(--font-display);
}
.query,
.form-grid,
.repair-row {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 14px;
  padding: 20px 24px;
  border: 0;
}
.delete-action {
  margin: 0 22px 20px;
  padding: 0;
  color: var(--color-danger);
  background: transparent;
  border: 0;
  font-size: 13px;
  font-weight: 800;
  cursor: pointer;
}
.query {
  grid-template-columns: 1fr auto;
  align-items: end;
}
.form-grid label,
.query label {
  display: grid;
  gap: 7px;
  color: var(--color-text-muted);
  font-size: 12px;
  font-weight: 700;
}
.form-grid input,
.form-grid select,
.query input,
.repair-row input {
  width: 100%;
  min-height: 44px;
  padding: 10px 12px;
  color: var(--color-ink);
  background: #fff;
  border: 1px solid var(--color-line-strong);
  border-radius: var(--radius-lg);
}
.form-grid input:focus-visible,
.form-grid select:focus-visible,
.query input:focus-visible,
.repair-row input:focus-visible {
  outline: 3px solid var(--color-focus);
  outline-offset: 1px;
}
.form-grid .wide {
  grid-column: 1 / -1;
}
.record-list {
  display: grid;
  gap: 10px;
  padding: 12px;
}
.record-list > button {
  display: grid;
  grid-template-columns: 55px 1fr auto 72px;
  align-items: center;
  gap: 12px;
  padding: 16px 18px;
  color: var(--color-ink);
  text-align: left;
  background: var(--color-brand-soft);
  border: 0;
  border-radius: var(--radius-lg);
  cursor: pointer;
}
.record-list > button:hover,
.record-list > button.selected {
  background: #e8f3ff;
  box-shadow: 0 12px 24px rgba(11, 99, 199, 0.1);
}
.record-list b,
.record-list small {
  font-family: var(--font-mono);
}
.record-list em {
  color: var(--color-brand-strong);
  font-style: normal;
  font-weight: 800;
}
.record-list p,
.table-list > p,
.cleaning-board > p {
  padding: 24px;
  color: var(--color-text-muted);
  font-size: 13px;
}
.actions-stack {
  display: grid;
  align-content: start;
  gap: 24px;
}
.status-actions {
  display: flex;
  gap: 8px;
  padding: 18px 24px 0;
  flex-wrap: wrap;
}
.repair-row {
  grid-template-columns: 1fr auto;
  border-bottom: 0;
}
.full-span {
  grid-column: 1 / -1;
}
.table-list > div {
  display: grid;
  grid-template-columns: 1fr 1.5fr 100px auto auto;
  align-items: center;
  gap: 12px;
  margin: 0 12px 10px;
  padding: 14px 16px;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.table-list span,
.table-list em {
  color: var(--color-text-muted);
  font-size: 12px;
  font-style: normal;
}
.single-desk {
  margin: 28px 0 0;
}
.cleaning-board {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(230px, 1fr));
  gap: 12px;
  padding: 12px;
}
.cleaning-board article {
  min-height: 190px;
  padding: 24px;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
}
.cleaning-board article > span {
  color: var(--color-brand-strong);
  font-family: var(--font-mono);
  font-weight: 800;
}
.cleaning-board h3 {
  margin: 24px 0 6px;
  color: var(--color-ink);
  font: 900 22px/1.2 var(--font-display);
}
.cleaning-board p,
.cleaning-board small {
  display: block;
  margin-bottom: 18px;
  color: var(--color-text-muted);
}
@media (max-width: 900px) {
  .work-grid {
    grid-template-columns: 1fr;
  }
  .full-span {
    grid-column: auto;
  }
  .table-list > div {
    grid-template-columns: 1fr 1fr;
  }
  .desk-tabs {
    overflow-x: auto;
  }
}
.cleaning-rail {
  display: flex;
  flex-direction: row;
  align-items: stretch;
  overflow-x: auto;
  padding: 14px 16px 10px;
  margin: 0;
  gap: 14px;
  scroll-snap-type: x proximity;
}
.cleaning-rail article {
  flex: 0 0 300px;
  scroll-snap-align: start;
}
.cleaning-rail > p {
  flex: none;
  width: 100%;
}
</style>
