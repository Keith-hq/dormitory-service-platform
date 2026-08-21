<template>
  <span class="status-tag" :class="[`status-tag--${tone}`, `status-tag--${size}`]">
    <span v-if="dot" class="status-dot" aria-hidden="true"></span>
    <slot>{{ label }}</slot>
  </span>
</template>

<script setup>
defineProps({
  label: { type: String, default: '' },
  tone: {
    type: String,
    default: 'neutral',
    validator: (value) =>
      ['neutral', 'info', 'success', 'warning', 'danger', 'rose'].includes(value)
  },
  size: {
    type: String,
    default: 'medium',
    validator: (value) => ['small', 'medium'].includes(value)
  },
  dot: { type: Boolean, default: true }
})
</script>

<style scoped>
.status-tag {
  --tag-color: var(--color-text-muted);
  --tag-bg: var(--color-surface-muted);
  --tag-border: var(--color-line);

  display: inline-flex;
  align-items: center;
  width: fit-content;
  border: 1px solid var(--tag-border);
  border-radius: 999px;
  background: var(--tag-bg);
  color: var(--tag-color);
  font-weight: 900;
  letter-spacing: 0;
  line-height: 1;
  white-space: nowrap;
}
.status-tag--small {
  gap: 5px;
  padding: 7px 11px;
  font-size: 13px;
}
.status-tag--medium {
  gap: 7px;
  padding: 9px 14px;
  font-size: 15px;
}
.status-dot {
  width: 7px;
  height: 7px;
  border-radius: 50%;
  background: currentColor;
  box-shadow: 0 0 0 3px color-mix(in srgb, currentColor 12%, transparent);
}
.status-tag--info {
  --tag-color: #075fa8;
  --tag-bg: #e8f3ff;
  --tag-border: #b9d8fb;
}
.status-tag--success {
  --tag-color: #007c73;
  --tag-bg: #e4f8f6;
  --tag-border: #b4e6e1;
}
.status-tag--warning {
  --tag-color: #9a6109;
  --tag-bg: #fff7e8;
  --tag-border: #f1d8a8;
}
.status-tag--danger {
  --tag-color: #a34239;
  --tag-bg: #fbeae6;
  --tag-border: #efc4bc;
}
.status-tag--rose {
  --tag-color: #9b5265;
  --tag-bg: #f9edf0;
  --tag-border: #e9c9d1;
}
</style>
