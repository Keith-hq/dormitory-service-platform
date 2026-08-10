<template>
  <div class="building-page">
    <h2>楼栋管理</h2>

    <!-- 搜索栏 -->
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

    <!-- 数据表格 -->
    <CrudTable
      :columns="columns"
      :data="list"
      :total="total"
      :loading="loading"
      :page="currentPage"
      row-key="buildingId"
      caption="楼栋列表"
      @create="openCreateModal"
      @edit="openEditModal"
      @delete="handleDelete"
      @page-change="onPageChange"
    />

    <!-- 新增/编辑弹窗 -->
    <div v-if="showModal" class="modal-overlay" @click.self="closeModal">
      <div class="modal">
        <h3>{{ isEdit ? '编辑楼栋' : '新增楼栋' }}</h3>
        <div class="form-group">
          <label>楼栋名称</label>
          <input v-model="form.buildingName" class="form-input" placeholder="如：1号楼" />
        </div>
        <div class="form-group">
          <label>楼栋类型</label>
          <select v-model="form.buildingType" class="form-select">
            <option value="男生宿舍">男生宿舍</option>
            <option value="女生宿舍">女生宿舍</option>
            <option value="混合宿舍">混合宿舍</option>
          </select>
        </div>
        <div class="form-group">
          <label>楼层数量</label>
          <input
            v-model.number="form.floorCount"
            type="number"
            class="form-input"
            min="1"
            max="50"
          />
        </div>
        <div class="modal-actions">
          <button class="btn" @click="closeModal">取消</button>
          <button class="btn btn-primary" :disabled="submitting" @click="submit">
            {{ submitting ? '提交中...' : '确定' }}
          </button>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, reactive, onMounted } from 'vue'
import { buildingApi } from '@/api/building'
import CrudTable from '@/components/CrudTable.vue'
import SearchForm from '@/components/SearchForm.vue'

// ===== 表格配置 =====
const columns = [
  { prop: 'buildingId', label: 'ID', width: '80px' },
  { prop: 'buildingName', label: '楼栋名称', width: '180px' },
  { prop: 'buildingType', label: '类型', width: '120px' },
  { prop: 'floorCount', label: '楼层数', width: '100px' },
  { prop: 'createTime', label: '创建时间', width: '180px' }
]

// ===== 数据状态 =====
const list = ref([])
const total = ref(0)
const loading = ref(false)
const currentPage = ref(1)
const pageSize = ref(10)
const filterType = ref('')

// ===== 弹窗状态 =====
const showModal = ref(false)
const isEdit = ref(false)
const editId = ref(null)
const submitting = ref(false)
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
  } catch (e) {
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
const openCreateModal = () => {
  isEdit.value = false
  editId.value = null
  form.buildingName = ''
  form.buildingType = '男生宿舍'
  form.floorCount = 6
  showModal.value = true
}

// ===== 编辑 =====
const openEditModal = (row) => {
  isEdit.value = true
  editId.value = row.buildingId
  form.buildingName = row.buildingName
  form.buildingType = row.buildingType
  form.floorCount = row.floorCount
  showModal.value = true
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
const closeModal = () => {
  showModal.value = false
}

// ===== 初始化 =====
onMounted(() => {
  fetchData()
})
</script>

<style scoped>
.building-page {
  max-width: 960px;
  margin: 0 auto;
  padding: 24px;
}
h2 {
  margin-bottom: 20px;
  font-size: 22px;
}
.form-select,
.form-input {
  padding: 6px 10px;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  font-size: 14px;
}
.filter-field {
  display: flex;
  align-items: center;
  gap: 8px;
  color: #475467;
  font-size: 13px;
  font-weight: 600;
}
.filter-field .form-select {
  min-width: 150px;
}
.form-select:disabled {
  cursor: not-allowed;
  opacity: 0.65;
}
.form-input {
  width: 100%;
}
.form-group {
  margin-bottom: 12px;
}
.form-group label {
  display: block;
  margin-bottom: 4px;
  font-size: 13px;
  color: #555;
}
/* 弹窗 */
.modal-overlay {
  position: fixed;
  inset: 0;
  background: rgba(0, 0, 0, 0.35);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}
.modal {
  background: #fff;
  border-radius: 8px;
  padding: 24px;
  width: 420px;
  box-shadow: 0 4px 20px rgba(0, 0, 0, 0.15);
}
.modal h3 {
  margin: 0 0 16px;
  font-size: 18px;
}
.modal-actions {
  display: flex;
  justify-content: flex-end;
  gap: 10px;
  margin-top: 20px;
}
.btn {
  cursor: pointer;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  padding: 6px 14px;
  background: #fff;
  font-size: 14px;
}
.btn-primary {
  background: #1890ff;
  color: #fff;
  border-color: #1890ff;
}
.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}
</style>
