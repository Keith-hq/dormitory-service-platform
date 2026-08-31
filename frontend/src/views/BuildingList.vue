<template>
  <div class="building-page">
    <PageHeader
      eyebrow="SPACE DIRECTORY"
      title="楼栋档案"
      description="统一维护宿舍楼栋类型、楼层与启用信息，为房间、床位和报修模块提供可靠的空间底座。"
    >
      <StatusTag v-if="mockEnabled" label="Mock 数据" tone="warning" />
      <StatusTag v-if="readonly" label="只读视图" tone="neutral" />
      <div class="page-stat" aria-label="当前楼栋记录数">
        <span>当前记录</span>
        <strong>{{ total }}</strong>
      </div>
    </PageHeader>

    <div class="workspace">
      <SearchForm :loading="loading" @search="fetchData" @reset="onReset">
        <template #default="{ disabled }">
          <label class="filter-field">
            <span>楼栋类型</span>
            <select v-model="filterType" class="form-select" :disabled="disabled">
              <option value="">全部类型</option>
              <option value="男生宿舍">男生宿舍</option>
              <option value="女生宿舍">女生宿舍</option>
              <option value="混合宿舍">混合宿舍</option>
            </select>
          </label>
        </template>
      </SearchForm>

      <CrudTable
        :columns="columns"
        :data="list"
        :total="total"
        :loading="loading"
        :page="currentPage"
        row-key="buildingId"
        caption="楼栋列表"
        :show-create="isSuperAdmin"
        :show-actions="isSuperAdmin"
        @create="openCreateModal"
        @edit="openEditModal"
        @delete="handleDelete"
        @page-change="onPageChange"
      >
        <template #cell-buildingType="{ value }">
          <StatusTag :label="value" :tone="buildingTypeTones[value] || 'neutral'" />
        </template>
      </CrudTable>
    </div>

    <Teleport to="body">
      <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
        <form
          ref="modalElement"
          class="modal"
          role="dialog"
          aria-modal="true"
          aria-labelledby="building-modal-title"
          tabindex="-1"
          @submit.prevent="submit"
          @keydown.esc.stop.prevent="closeModal"
          @keydown.tab="trapModalFocus"
        >
          <header class="modal-header">
            <div>
              <p class="modal-kicker">BUILDING RECORD</p>
              <h2 id="building-modal-title">{{ isEdit ? '编辑楼栋' : '新增楼栋' }}</h2>
            </div>
            <button class="modal-close" type="button" aria-label="关闭弹窗" @click="closeModal">
              ×
            </button>
          </header>

          <div class="modal-fields">
            <div class="form-group">
              <label for="building-name">楼栋名称</label>
              <input
                id="building-name"
                ref="buildingNameInput"
                v-model="form.buildingName"
                class="form-input"
                placeholder="如：1号楼"
                required
              />
            </div>
            <div class="form-group">
              <label for="building-type">楼栋类型</label>
              <select id="building-type" v-model="form.buildingType" class="form-select">
                <option value="男生宿舍">男生宿舍</option>
                <option value="女生宿舍">女生宿舍</option>
                <option value="混合宿舍">混合宿舍</option>
              </select>
            </div>
            <div class="form-group">
              <label for="floor-count">楼层数量</label>
              <input
                id="floor-count"
                v-model.number="form.floorCount"
                type="number"
                class="form-input"
                min="1"
                max="50"
                required
              />
            </div>
          </div>

          <footer class="modal-actions">
            <button class="btn" type="button" @click="closeModal">取消</button>
            <button class="btn btn-primary" type="submit" :disabled="submitting">
              {{ submitting ? '提交中…' : isEdit ? '保存修改' : '创建档案' }}
            </button>
          </footer>
        </form>
      </div>
    </Teleport>
  </div>
</template>

<script setup>
import { computed, nextTick, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { buildingApi } from '@/api/building'
import { useUserStore } from '@/store/user'
import { CrudTable, PageHeader, SearchForm, StatusTag } from '@/components'

// ===== 表格配置 =====
const columns = [
  { prop: 'buildingId', label: 'ID', width: '80px' },
  { prop: 'buildingName', label: '楼栋名称', width: '180px' },
  { prop: 'buildingType', label: '类型', width: '120px' },
  { prop: 'floorCount', label: '楼层数', width: '100px' },
  { prop: 'createTime', label: '创建时间', width: '180px' }
]

const buildingTypeTones = {
  男生宿舍: 'info',
  女生宿舍: 'rose',
  混合宿舍: 'warning'
}

// ===== 数据状态 =====
const list = ref([])
const total = ref(0)
const loading = ref(false)
const currentPage = ref(1)
const pageSize = ref(10)
const filterType = ref('')
const mockEnabled = import.meta.env.DEV && import.meta.env.VITE_USE_MOCK === 'true'

// ===== 权限与楼栋作用域 =====
// 超管可增删改全部楼栋；宿管（楼长/维修员）仅可查看自己负责的楼栋档案
const userStore = useUserStore()
const isSuperAdmin = computed(() => userStore.userInfo?.role === 'super_admin')
const myBuildingId = Number(userStore.userInfo?.buildingId) || null
const readonly = computed(() => !isSuperAdmin.value)

// ===== 弹窗状态 =====
const showModal = ref(false)
const isEdit = ref(false)
const editId = ref(null)
const submitting = ref(false)
const modalElement = ref(null)
const buildingNameInput = ref(null)
let modalTrigger = null
let inertBackground = null
let backgroundWasInert = false
const form = reactive({
  buildingName: '',
  buildingType: '男生宿舍',
  floorCount: 6
})

// ===== 数据获取 =====
const fetchData = async () => {
  loading.value = true
  try {
    const requestList = () =>
      buildingApi.getList({
        page: currentPage.value,
        pageSize: pageSize.value,
        buildingType: filterType.value || undefined
      })

    let data = await requestList()
    const lastPage = Math.max(1, Math.ceil((Number(data?.total) || 0) / pageSize.value))

    if (currentPage.value > lastPage) {
      currentPage.value = lastPage
      data = await requestList()
    }

    // 统一返回格式 { items, total }
    list.value = data?.items || []
    total.value = data?.total || 0

    // 宿管仅显示自己负责的楼栋档案（只读）
    if (myBuildingId) {
      list.value = list.value.filter((item) => Number(item.buildingId) === myBuildingId)
      total.value = list.value.length
    }
  } catch (e) {
    if (e.code === 401 || e.status === 401) return
    console.error('获取楼栋列表失败:', e)
    alert('获取数据失败，请确认后端已启动')
  } finally {
    loading.value = false
  }
}

// ===== 分页 =====
const onPageChange = ({ page, pageSize: size }) => {
  currentPage.value = page
  pageSize.value = size
  fetchData()
}

// ===== 搜索重置 =====
const onReset = () => {
  filterType.value = ''
  currentPage.value = 1
}

// ===== 新增 =====
const isolateBackground = () => {
  const appElement = document.querySelector('#app')
  if (!appElement || inertBackground) return

  inertBackground = appElement
  backgroundWasInert = appElement.inert
  appElement.inert = true
}

const restoreBackground = () => {
  if (!inertBackground) return

  inertBackground.inert = backgroundWasInert
  inertBackground = null
  backgroundWasInert = false
}

const focusModal = () => {
  isolateBackground()
  nextTick(() => buildingNameInput.value?.focus())
}

const openCreateModal = () => {
  modalTrigger = document.activeElement
  isEdit.value = false
  editId.value = null
  form.buildingName = ''
  form.buildingType = '男生宿舍'
  form.floorCount = 6
  showModal.value = true
  focusModal()
}

// ===== 编辑 =====
const openEditModal = (row) => {
  modalTrigger = document.activeElement
  isEdit.value = true
  editId.value = row.buildingId
  form.buildingName = row.buildingName
  form.buildingType = row.buildingType
  form.floorCount = row.floorCount
  showModal.value = true
  focusModal()
}

// ===== 提交 =====
const submit = async () => {
  if (!form.buildingName.trim()) return alert('请输入楼栋名称')

  submitting.value = true
  try {
    if (isEdit.value) {
      await buildingApi.update(editId.value, {
        buildingName: form.buildingName,
        buildingType: form.buildingType,
        floorCount: form.floorCount
      })
    } else {
      await buildingApi.create({
        buildingName: form.buildingName,
        buildingType: form.buildingType,
        floorCount: form.floorCount
      })
    }
    closeModal()
    fetchData()
  } catch (e) {
    alert('操作失败: ' + e.message)
  } finally {
    submitting.value = false
  }
}

// ===== 删除 =====
const handleDelete = async (row) => {
  if (!confirm(`确认删除"${row.buildingName}"吗？此操作不可恢复。`)) return

  try {
    await buildingApi.delete(row.buildingId)
    fetchData()
  } catch (e) {
    alert('删除失败: ' + e.message)
  }
}

// ===== 弹窗 =====
const focusableSelector = [
  'button:not([disabled])',
  '[href]',
  'input:not([disabled])',
  'select:not([disabled])',
  'textarea:not([disabled])',
  '[tabindex]:not([tabindex="-1"])'
].join(',')

const trapModalFocus = (event) => {
  const modal = modalElement.value
  if (!modal) return

  const focusableElements = [...modal.querySelectorAll(focusableSelector)].filter(
    (element) => element.getAttribute('aria-hidden') !== 'true'
  )

  if (focusableElements.length === 0) {
    event.preventDefault()
    modal.focus()
    return
  }

  const firstElement = focusableElements[0]
  const lastElement = focusableElements.at(-1)
  const activeElement = document.activeElement

  if (event.shiftKey && (activeElement === firstElement || !modal.contains(activeElement))) {
    event.preventDefault()
    lastElement.focus()
  } else if (!event.shiftKey && activeElement === lastElement) {
    event.preventDefault()
    firstElement.focus()
  }
}

const closeModal = () => {
  if (!showModal.value) return
  showModal.value = false
  restoreBackground()
  nextTick(() => {
    modalTrigger?.focus?.()
    modalTrigger = null
  })
}

// ===== 初始化 =====
onMounted(() => {
  fetchData()
})

onBeforeUnmount(() => {
  restoreBackground()
})
</script>

<style scoped>
.building-page {
  width: min(100% - 48px, var(--content-max));
  margin: 0 auto;
  padding-bottom: var(--space-9);
}

.page-stat {
  display: flex;
  min-width: 116px;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-3);
  min-height: 52px;
  padding: 0 16px;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}

.page-stat span {
  color: var(--color-text-muted);
  font-size: 11px;
  font-weight: 800;
  letter-spacing: 0.08em;
}

.page-stat strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 30px;
  line-height: 1;
}

.workspace {
  display: grid;
  gap: var(--space-5);
  padding-top: var(--space-6);
}

.building-page :deep(.search-form),
.building-page :deep(.crud-table) {
  border: 0;
  border-radius: var(--radius-lg);
  background: #fff;
  box-shadow: var(--shadow-soft);
}

.filter-field {
  display: flex;
  align-items: center;
  gap: var(--space-3);
  color: var(--color-text);
  font-size: 13px;
  font-weight: 700;
}

.filter-field .form-select {
  min-width: 164px;
}

.form-group {
  display: grid;
  gap: var(--space-2);
}

.form-group label {
  color: var(--color-text);
  font-size: 13px;
  font-weight: 750;
}

.form-group .form-input,
.form-group .form-select {
  width: 100%;
}

.modal-overlay {
  position: fixed;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: var(--space-5);
  background: rgba(15, 54, 108, 0.34);
  backdrop-filter: blur(6px);
  z-index: 100;
}

.modal {
  width: min(460px, 100%);
  overflow: hidden;
  border: 0;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: var(--shadow-lift);
}

.modal-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-5);
  padding: var(--space-6) var(--space-6) var(--space-5);
}

.modal-kicker {
  margin: 0 0 var(--space-2);
  color: var(--color-brand-strong);
  font-size: 10px;
  font-weight: 800;
  letter-spacing: 0.16em;
}

.modal h2 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 25px;
}

.modal-close {
  width: 34px;
  height: 34px;
  flex: 0 0 auto;
  border: 0;
  border-radius: 50%;
  background: var(--color-surface-muted);
  color: var(--color-text-muted);
  font-size: 23px;
  line-height: 1;
  cursor: pointer;
}

.modal-close:hover {
  border-color: var(--color-brand-border);
  color: var(--color-brand-strong);
}

.modal-close:focus-visible {
  outline: 3px solid var(--color-focus);
}

.modal-fields {
  display: grid;
  gap: var(--space-5);
  padding: var(--space-6);
}

.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: var(--space-3);
  padding: var(--space-4) var(--space-6) var(--space-6);
  background: #fff;
}

@media (max-width: 640px) {
  .building-page {
    width: min(calc(100% - 28px), var(--content-max));
  }

  .page-stat {
    justify-content: flex-start;
    padding-left: 0;
    border-left: 0;
  }

  .filter-field {
    align-items: stretch;
    flex-direction: column;
  }

  .filter-field .form-select {
    width: 100%;
  }

  .modal-overlay {
    align-items: flex-end;
    padding: 0;
  }

  .modal {
    width: 100%;
    border-radius: var(--radius-lg) var(--radius-lg) 0 0;
  }
}
</style>
