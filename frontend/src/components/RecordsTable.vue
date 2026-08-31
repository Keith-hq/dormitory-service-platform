<template>
  <section class="records-panel" :aria-busy="loading">
    <header class="records-panel__header">
      <div>
        <span>{{ eyebrow }}</span>
        <h2>{{ title }}</h2>
      </div>
      <slot name="header"></slot>
    </header>

    <div class="records-panel__body">
      <table v-if="items.length" class="records-table">
        <caption class="sr-only">
          {{
            title
          }}
        </caption>
        <thead>
          <tr>
            <th v-for="column in columns" :key="column.key">{{ column.label }}</th>
            <th v-if="$slots.actions" class="records-table__actions-heading">操作</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="(item, index) in items" :key="getRowKey(item, index)">
            <td v-for="column in columns" :key="column.key">
              <slot :name="`cell-${column.key}`" :item="item" :value="item[column.key]">
                {{ item[column.key] ?? '—' }}
              </slot>
            </td>
            <td v-if="$slots.actions" class="records-table__actions">
              <slot name="actions" :item="item"></slot>
            </td>
          </tr>
        </tbody>
      </table>

      <div v-else-if="!loading" class="records-empty">
        <span aria-hidden="true">✓</span>
        <strong>{{ emptyText }}</strong>
        <small>当前没有需要处理的记录</small>
      </div>

      <div v-if="loading" class="records-loading" role="status" aria-live="polite">
        <span aria-hidden="true"></span>
        正在同步数据…
      </div>
    </div>
  </section>
</template>

<script setup>
const props = defineProps({
  eyebrow: { type: String, default: 'LIVE REGISTER' },
  title: { type: String, required: true },
  columns: { type: Array, required: true },
  items: { type: Array, default: () => [] },
  loading: { type: Boolean, default: false },
  rowKey: { type: [String, Function], default: 'id' },
  emptyText: { type: String, default: '暂无记录' }
})

const getRowKey = (item, index) => {
  const key = typeof props.rowKey === 'function' ? props.rowKey(item) : item?.[props.rowKey]
  return key ?? index
}
</script>

<style scoped>
.records-panel {
  overflow: hidden;
  border-radius: var(--radius-lg);
  background: var(--color-surface);
  box-shadow: 0 16px 42px rgba(23, 65, 120, 0.1);
}

.records-panel__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 96px;
  gap: var(--space-4);
  padding: 28px 36px 18px;
}

.records-panel__header > div {
  display: grid;
  gap: 3px;
}

.records-panel__header span {
  color: var(--color-brand);
  font-size: 15px;
  font-weight: 850;
  letter-spacing: 0;
}

.records-panel__header h2 {
  margin: 0;
  color: var(--color-ink);
  font-family: var(--font-display);
  font-size: 30px;
  font-weight: 950;
}

.records-panel__body {
  position: relative;
  overflow-x: auto;
  padding: 0 36px 34px;
}

.records-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0 14px;
  font-size: 16px;
}

.records-table th,
.records-table td {
  padding: 20px 24px;
  text-align: left;
  white-space: nowrap;
}

.records-table th {
  background: transparent;
  color: var(--color-brand);
  font-size: 16px;
  font-weight: 850;
  letter-spacing: 0;
}

.records-table tbody td {
  background: #f4f7fc;
  color: var(--color-text-muted);
}

.records-table tbody td:first-child {
  border-radius: var(--radius-lg) 0 0 var(--radius-lg);
}

.records-table tbody td:last-child {
  border-radius: 0 var(--radius-lg) var(--radius-lg) 0;
}

.records-table tbody tr:hover {
  background: transparent;
}

.records-table tbody tr:hover td {
  background: #eef4ff;
}

.records-table__actions-heading,
.records-table__actions {
  width: 190px;
  text-align: right !important;
}

.records-table__actions :deep(.btn + .btn) {
  margin-left: 6px;
}

.records-empty,
.records-loading {
  display: grid;
  min-height: 260px;
  place-content: center;
  place-items: center;
  gap: var(--space-2);
  color: var(--color-text-muted);
  font-size: 19px;
}

.records-empty > span {
  display: grid;
  width: 52px;
  height: 52px;
  place-items: center;
  border-radius: var(--radius-lg);
  background: var(--color-brand-soft);
  color: var(--color-brand-strong);
  font-weight: 800;
}

.records-empty small {
  color: var(--color-text-soft);
  font-size: 14px;
}

.records-loading > span {
  width: 22px;
  height: 22px;
  border: 2px solid var(--color-brand-border);
  border-top-color: var(--color-brand);
  border-radius: 50%;
  animation: record-spin 0.8s linear infinite;
}

@keyframes record-spin {
  to {
    transform: rotate(360deg);
  }
}
</style>
