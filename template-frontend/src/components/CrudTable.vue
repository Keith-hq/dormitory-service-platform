<template>
  <div class="crud-table">
    <!-- 操作按钮区 -->
    <div class="toolbar">
      <button
        v-if="showCreate"
        type="button"
        class="btn btn-primary"
        :disabled="loading"
        @click="$emit('create')"
      >
        {{ createText }}
      </button>
      <slot name="toolbar"></slot>
    </div>

    <!-- 数据表格 -->
    <table v-if="data.length > 0" class="table">
      <caption class="sr-only">
        {{
          caption
        }}
      </caption>
      <thead>
        <tr>
          <th v-for="col in columns" :key="col.prop" :style="{ width: col.width }">
            {{ col.label }}
          </th>
          <th v-if="showActions" style="width: 160px">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, index) in data" :key="getRowKey(row, index)">
          <td v-for="col in columns" :key="col.prop">
            <slot :name="'cell-' + col.prop" :row="row" :value="row[col.prop]">
              {{ row[col.prop] }}
            </slot>
          </td>
          <td v-if="showActions" class="actions">
            <slot name="actions" :row="row">
              <button
                type="button"
                class="btn btn-sm btn-edit"
                :disabled="loading"
                @click="$emit('edit', row)"
              >
                编辑
              </button>
              <button
                type="button"
                class="btn btn-sm btn-danger"
                :disabled="loading"
                @click="$emit('delete', row)"
              >
                删除
              </button>
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
    <div v-else-if="!loading" class="empty">{{ emptyText }}</div>
    <div v-if="loading" class="loading" role="status" aria-live="polite">加载中...</div>

    <!-- 分页 -->
    <div v-if="total > 0" class="pagination">
      <span>共 {{ total }} 条</span>
      <button
        type="button"
        class="btn btn-sm"
        :disabled="loading || page <= 1"
        @click="goPage(page - 1)"
      >
        上一页
      </button>
      <span>{{ page }} / {{ totalPages }}</span>
      <button
        type="button"
        class="btn btn-sm"
        :disabled="loading || page >= totalPages"
        @click="goPage(page + 1)"
      >
        下一页
      </button>
    </div>
  </div>
</template>

<script setup>
import { computed } from 'vue'

const props = defineProps({
  columns: { type: Array, required: true },
  data: { type: Array, default: () => [] },
  total: { type: Number, default: 0 },
  loading: { type: Boolean, default: false },
  page: { type: Number, default: 1 },
  pageSize: { type: Number, default: 10 },
  rowKey: { type: [String, Function], default: 'id' },
  caption: { type: String, default: '数据列表' },
  emptyText: { type: String, default: '暂无数据' },
  createText: { type: String, default: '+ 新增' },
  showCreate: { type: Boolean, default: true },
  showActions: { type: Boolean, default: true }
})

const emit = defineEmits(['create', 'edit', 'delete', 'page-change'])

const totalPages = computed(() => Math.max(1, Math.ceil(props.total / props.pageSize)))

const getRowKey = (row, index) => {
  const key = typeof props.rowKey === 'function' ? props.rowKey(row) : row?.[props.rowKey]
  return key ?? index
}

const goPage = (page) => {
  const targetPage = Math.min(Math.max(1, page), totalPages.value)
  if (targetPage === props.page || props.loading) return
  emit('page-change', { page: targetPage, pageSize: props.pageSize })
}
</script>

<style scoped>
.crud-table {
  padding: 16px;
}
.toolbar {
  margin-bottom: 12px;
}
.table {
  width: 100%;
  border-collapse: collapse;
  background: #fff;
  border: 1px solid #e8e8e8;
}
.table th,
.table td {
  padding: 10px 12px;
  border-bottom: 1px solid #e8e8e8;
  text-align: left;
}
.table th {
  background: #fafafa;
  font-weight: 600;
}
.table tbody tr:hover {
  background: #f5f5f5;
}
.actions {
  white-space: nowrap;
}
.btn {
  cursor: pointer;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  padding: 6px 14px;
  background: #fff;
  font-size: 14px;
}
.btn:disabled {
  cursor: not-allowed;
  opacity: 0.55;
}
.btn-primary {
  background: #1890ff;
  color: #fff;
  border-color: #1890ff;
}
.btn-sm {
  padding: 4px 10px;
  font-size: 12px;
}
.btn-edit {
  color: #1890ff;
  border-color: #1890ff;
  margin-right: 6px;
}
.btn-danger {
  color: #ff4d4f;
  border-color: #ff4d4f;
}
.pagination {
  margin-top: 16px;
  display: flex;
  align-items: center;
  gap: 12px;
  font-size: 14px;
}
.empty,
.loading {
  text-align: center;
  padding: 48px 0;
  color: #999;
  font-size: 14px;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  margin: -1px;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}
</style>
