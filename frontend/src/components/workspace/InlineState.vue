<script setup>
defineProps({
  loading: { type: Boolean, default: false },
  error: { type: String, default: '' },
  empty: { type: Boolean, default: false },
  emptyText: { type: String, default: '暂无记录' }
})
</script>

<template>
  <div
    v-if="loading || error || empty"
    class="inline-state"
    :class="{ 'inline-state--error': error }"
    role="status"
  >
    <span>{{ loading ? 'SYNC' : error ? 'ERROR' : 'EMPTY' }}</span>
    <p>{{ loading ? '正在同步数据…' : error || emptyText }}</p>
  </div>
</template>

<style scoped>
.inline-state {
  display: flex;
  min-height: 94px;
  align-items: center;
  justify-content: center;
  flex-direction: column;
  padding: 20px;
  border: 1px dashed var(--color-line-strong);
  background: rgba(255, 255, 255, 0.2);
  color: var(--color-text-muted);
  gap: 7px;
}
.inline-state span {
  color: var(--color-accent-strong);
  font: 8px var(--font-mono);
  letter-spacing: 0.16em;
}
.inline-state p {
  margin: 0;
  font-size: 11px;
}
.inline-state--error {
  border-color: #d8a99d;
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
</style>
