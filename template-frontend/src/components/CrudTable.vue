<template>
  <div class="crud-table">
    <!-- 操作按钮区 -->
    <div class="toolbar">
      <button class="btn btn-primary" @click="$emit('create')">+ 新增</button>
      <slot name="toolbar"></slot>
    </div>

    <!-- 数据表格 -->
<<<<<<< HEAD
    <table class="table" v-if="data.length > 0">
=======
    <table v-if="data.length > 0" class="table">
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
      <thead>
        <tr>
          <th v-for="col in columns" :key="col.prop" :style="{ width: col.width }">
            {{ col.label }}
          </th>
          <th style="width: 160px">操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="(row, idx) in data" :key="idx">
          <td v-for="col in columns" :key="col.prop">
            <slot :name="'cell-' + col.prop" :row="row" :value="row[col.prop]">
              {{ row[col.prop] }}
            </slot>
          </td>
          <td class="actions">
            <button class="btn btn-sm btn-edit" @click="$emit('edit', row)">编辑</button>
            <button class="btn btn-sm btn-danger" @click="$emit('delete', row)">删除</button>
          </td>
        </tr>
      </tbody>
    </table>
    <div v-else-if="!loading" class="empty">暂无数据</div>
    <div v-if="loading" class="loading">加载中...</div>

    <!-- 分页 -->
<<<<<<< HEAD
    <div class="pagination" v-if="total > 0">
      <span>共 {{ total }} 条</span>
      <button
        class="btn btn-sm"
        :disabled="currentPage <= 1"
        @click="goPage(currentPage - 1)"
      >
=======
    <div v-if="total > 0" class="pagination">
      <span>共 {{ total }} 条</span>
      <button class="btn btn-sm" :disabled="currentPage <= 1" @click="goPage(currentPage - 1)">
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
        上一页
      </button>
      <span>{{ currentPage }} / {{ Math.ceil(total / pageSize) }}</span>
      <button
        class="btn btn-sm"
        :disabled="currentPage >= Math.ceil(total / pageSize)"
        @click="goPage(currentPage + 1)"
      >
        下一页
      </button>
    </div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const props = defineProps({
<<<<<<< HEAD
  columns:  { type: Array, required: true },
  data:     { type: Array, default: () => [] },
  total:    { type: Number, default: 0 },
  loading:  { type: Boolean, default: false },
=======
  columns: { type: Array, required: true },
  data: { type: Array, default: () => [] },
  total: { type: Number, default: 0 },
  loading: { type: Boolean, default: false },
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
  pageSize: { type: Number, default: 10 }
})

const emit = defineEmits(['create', 'edit', 'delete', 'page-change'])

const currentPage = ref(1)

const goPage = (page) => {
  currentPage.value = page
  emit('page-change', { page, pageSize: props.pageSize })
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
<<<<<<< HEAD
.table th, .table td {
=======
.table th,
.table td {
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
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
<<<<<<< HEAD
.empty, .loading {
=======
.empty,
.loading {
>>>>>>> b275658530f4ec665d7608e04a9c6a80140ce075
  text-align: center;
  padding: 48px 0;
  color: #999;
  font-size: 14px;
}
</style>
