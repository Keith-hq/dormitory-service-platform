<template>
  <section class="crud-table" :aria-busy="loading">
    <header class="toolbar">
      <div class="toolbar__heading">
        <span>DATA REGISTER</span>
        <strong>{{ caption }}</strong>
      </div>
      <div class="toolbar__actions">
        <button
          v-if="showCreate"
          type="button"
          class="btn btn-primary"
          :disabled="loading"
          @click="$emit('create')"
        >
          <span aria-hidden="true">＋</span>
          {{ createText.replace(/^\+\s*/, '') }}
        </button>
        <slot name="toolbar"></slot>
      </div>
    </header>

    <div class="table-frame">
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
            <th v-if="showActions" class="actions-heading">操作</th>
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
                  class="btn btn-sm btn-ghost"
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

      <div v-else-if="!loading" class="empty">
        <span class="empty__mark" aria-hidden="true">空</span>
        <strong>{{ emptyText }}</strong>
        <small>调整筛选条件或新增一条记录</small>
      </div>
      <div
        v-if="loading"
        class="loading"
        :class="{ 'loading--overlay': data.length > 0 }"
        role="status"
        aria-live="polite"
      >
        <span class="loading__dot" aria-hidden="true"></span>
        正在整理数据…
      </div>
    </div>

    <footer v-if="total > 0" class="pagination">
      <span class="pagination__summary"
        >共 <strong>{{ total }}</strong> 条记录</span
      >
      <div class="pagination__controls">
        <button
          type="button"
          class="btn btn-sm"
          :disabled="loading || page <= 1"
          aria-label="上一页"
          @click="goPage(page - 1)"
        >
          ←
        </button>
        <span class="pagination__current"
          >{{ page }} <small>/ {{ totalPages }}</small></span
        >
        <button
          type="button"
          class="btn btn-sm"
          :disabled="loading || page >= totalPages"
          aria-label="下一页"
          @click="goPage(page + 1)"
        >
          →
        </button>
      </div>
    </footer>
  </section>
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
  overflow: hidden;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}

.toolbar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-5);
  min-height: 96px;
  padding: 28px 36px 18px;
}

.toolbar__heading {
  display: grid;
  gap: 2px;
  text-align: left;
}

.toolbar__heading span {
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}

.toolbar__heading strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 26px;
  font-weight: 950;
}

.toolbar__actions {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: var(--space-2);
}

.table-frame {
  position: relative;
  overflow-x: auto;
  padding: 0 36px 22px;
}

.table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0 14px;
  background: transparent;
  font-size: 16px;
}

.table th,
.table td {
  padding: 19px 22px;
  text-align: left;
  white-space: nowrap;
}

.table th {
  background: transparent;
  color: var(--color-brand);
  font-size: 16px;
  font-weight: 850;
  letter-spacing: 0;
  text-transform: none;
}

.table tbody td {
  background: #f4f7fc;
  color: var(--color-text-muted);
}

.table tbody td:first-child {
  border-radius: var(--radius-lg) 0 0 var(--radius-lg);
}

.table tbody td:last-child {
  border-radius: 0 var(--radius-lg) var(--radius-lg) 0;
}

.table tbody tr {
  transition: background 0.16s ease;
}

.table tbody tr:hover {
  background: transparent;
}

.table tbody tr:hover td {
  background: #eef4ff;
}

.actions-heading {
  width: 156px;
}

.actions {
  width: 156px;
  white-space: nowrap;
}

.pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  min-height: 66px;
  padding: 16px 36px 30px;
  background: transparent;
  color: var(--color-text-muted);
  font-size: 14px;
}

.pagination__summary strong,
.pagination__current {
  color: var(--color-ink);
  font-weight: 800;
}

.pagination__controls {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}

.pagination__current {
  min-width: 58px;
  text-align: center;
}

.pagination__current small {
  color: var(--color-text-soft);
  font-weight: 600;
}

.empty,
.loading {
  display: grid;
  min-height: 260px;
  place-content: center;
  place-items: center;
  gap: var(--space-2);
  text-align: center;
  color: var(--color-text-muted);
  font-size: 16px;
}

.empty__mark {
  display: grid;
  width: 52px;
  height: 52px;
  margin-bottom: var(--space-2);
  place-items: center;
  border: 1px solid var(--color-brand-border);
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-family: var(--font-display);
  font-size: 17px;
}

.empty strong {
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 20px;
  font-weight: 900;
}

.empty small,
.loading {
  font-size: 12px;
}

.loading--overlay {
  position: absolute;
  z-index: 1;
  inset: 0;
  min-height: 0;
  background: rgba(255, 253, 248, 0.82);
  backdrop-filter: blur(2px);
}

.loading__dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: var(--color-brand);
  box-shadow:
    14px 0 0 var(--color-brand-border),
    -14px 0 0 var(--color-brand-border);
  animation: loading-pulse 1s ease-in-out infinite alternate;
}

@keyframes loading-pulse {
  to {
    opacity: 0.45;
    transform: scale(0.82);
  }
}

@media (max-width: 640px) {
  .toolbar,
  .pagination {
    align-items: stretch;
    flex-direction: column;
  }

  .toolbar__actions,
  .pagination__controls {
    justify-content: space-between;
  }

  .toolbar__actions .btn-primary {
    flex: 1;
  }
}
</style>
