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
  border: 1px solid var(--color-line);
  border-radius: var(--radius-lg);
  background: rgba(255, 255, 255, 0.7);
  color: var(--color-text-muted);
  gap: 7px;
}
.inline-state span {
  color: var(--color-accent-strong);
  font-size: 12px;
  font-weight: 900;
  letter-spacing: 0;
}
.inline-state p {
  margin: 0;
  font-size: 14px;
  font-weight: 700;
}
.inline-state--error {
  border-color: #d8a99d;
  background: var(--color-danger-soft);
  color: var(--color-danger);
}
</style>
